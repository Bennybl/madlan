using System.Globalization;
using Microsoft.Data.Sqlite;

namespace MadlanExplorer;

public class SqliteDealRepository : IDealRepository
{
    private const int MinimumMetricContributorCount = 5;

    private const string FilterPredicate = """
        ConflictStatus = 'usable'
          AND ($city IS NULL OR City = $city)
          AND ($neighborhood IS NULL OR Neighborhood = $neighborhood)
          AND ($propertyType IS NULL OR PropertyType = $propertyType)
          AND ($minimumRooms IS NULL OR Rooms >= $minimumRooms)
          AND ($maximumRooms IS NULL OR Rooms <= $maximumRooms)
          AND ($startDate IS NULL OR DealDateStart >= $startDate)
          AND ($endDate IS NULL OR DealDateEnd <= $endDate)
          AND ($minimumFloor IS NULL OR Floor >= $minimumFloor)
          AND ($maximumFloor IS NULL OR Floor <= $maximumFloor)
          AND ($minimumYearBuilt IS NULL OR YearBuilt >= $minimumYearBuilt)
          AND ($maximumYearBuilt IS NULL OR YearBuilt <= $maximumYearBuilt)
          AND ($condition IS NULL OR Condition = $condition)
          AND ($source IS NULL OR Source = $source)
          AND ($hasElevator IS NULL OR HasElevator = $hasElevator)
          AND ($hasParking IS NULL OR HasParking = $hasParking)
          AND ($hasBalcony IS NULL OR HasBalcony = $hasBalcony)
          AND ($hasSafeRoom IS NULL OR HasSafeRoom = $hasSafeRoom)
        """;

    private readonly DatasetStore _datasetStore;

    public SqliteDealRepository(DatasetStore datasetStore)
    {
        _datasetStore = datasetStore;
    }

    public DealQueryResult Execute(DealQuery query)
    {
        var filters = query.Filters;
        var evidencePageSize = query.EvidencePageSize;
        using var connection = _datasetStore.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            WITH {BuildFilteredCte()},
            Prices AS (SELECT PriceNis AS Value FROM Filtered WHERE PriceNis > 0),
            RankedPrices AS (SELECT Value, ROW_NUMBER() OVER (ORDER BY Value) AS Position, COUNT(*) OVER () AS Total FROM Prices),
            Ratios AS (SELECT PriceNis / SizeSqm AS Value FROM Filtered WHERE PriceNis > 0 AND SizeSqm > 0),
            RankedRatios AS (SELECT Value, ROW_NUMBER() OVER (ORDER BY Value) AS Position, COUNT(*) OVER () AS Total FROM Ratios)
            SELECT
                (SELECT COUNT(*) FROM Filtered),
                (SELECT AVG(Value) FROM RankedPrices WHERE Position IN ((Total + 1) / 2, (Total + 2) / 2)),
                (SELECT AVG(Value) FROM RankedRatios WHERE Position IN ((Total + 1) / 2, (Total + 2) / 2)),
                (SELECT COUNT(*) FROM Prices),
                (SELECT COUNT(*) FROM Ratios),
                EXISTS (SELECT 1 FROM Prices WHERE Value < 100000),
                EXISTS (
                    SELECT 1
                    FROM Filtered
                    WHERE PriceNis > 0
                      AND SizeSqm > 0
                      AND SuppliedPricePerSqm IS NOT NULL
                      AND ABS(SuppliedPricePerSqm - (PriceNis / SizeSqm)) > MAX(1, (PriceNis / SizeSqm) * 0.01));
            """;
        AddFilterParameters(command, filters);
        using var reader = command.ExecuteReader();
        reader.Read();
        var transactionCount = reader.GetInt32(0);
        decimal? medianPriceNis = reader.IsDBNull(1) ? null : Convert.ToDecimal(reader.GetDouble(1));
        decimal? medianPricePerSqm = reader.IsDBNull(2) ? null : Convert.ToDecimal(reader.GetDouble(2));
        var priceContributorCount = reader.GetInt32(3);
        var pricePerSqmContributorCount = reader.GetInt32(4);
        var warnings = new List<string>();
        if (reader.GetInt64(5) == 1)
        {
            warnings.Add("low_price_reported");
        }

        if (priceContributorCount < MinimumMetricContributorCount)
        {
            warnings.Add("price_metric_has_fewer_than_five_contributors");
        }

        if (pricePerSqmContributorCount < MinimumMetricContributorCount)
        {
            warnings.Add("price_per_sqm_metric_has_fewer_than_five_contributors");
        }

        if (reader.GetInt64(6) == 1)
        {
            warnings.Add("supplied_price_per_sqm_mismatch");
        }

        reader.Dispose();
        var evidence = ReadEvidence(connection, filters, evidencePageSize);

        var requestedMetrics = new List<RequestedMetricResult>();
        foreach (var metric in query.Metrics.Distinct())
        {
            if (metric == QueryMetric.TransactionCount)
            {
                continue;
            }

            var (value, dealId) = ComputeRequestedMetric(connection, filters, metric);
            requestedMetrics.Add(new RequestedMetricResult { Metric = metric, Value = value, DealId = dealId });
        }

        var rankedMetrics = new List<RankedMetricResult>();
        foreach (var request in query.RankedMetrics)
        {
            var (value, dealId) = ComputeRankedMetric(connection, filters, request.Metric, request.Rank);
            rankedMetrics.Add(new RankedMetricResult { Metric = request.Metric, Rank = request.Rank, Value = value, DealId = dealId });
        }

        var groups = query.GroupBy is { } groupByField
            ? ComputeGroups(connection, filters, groupByField, query.Metrics, query.RankedMetrics)
            : [];

        var outliers = query.OutlierField is { } outlierField
            ? ComputeOutliers(connection, filters, outlierField, query.GroupBy)
            : [];

        return new DealQueryResult
        {
            TransactionCount = transactionCount,
            MedianPriceNis = medianPriceNis,
            MedianPricePerSqm = medianPricePerSqm,
            PriceContributorCount = priceContributorCount,
            PricePerSqmContributorCount = pricePerSqmContributorCount,
            ContributorDealIds = evidence.DealIds,
            PriceContributorDealIds = evidence.DealIds,
            PricePerSqmContributorDealIds = evidence.DealIds,
            HasMoreEvidence = evidence.HasMore,
            Warnings = warnings,
            RequestedMetrics = requestedMetrics,
            RankedMetrics = rankedMetrics,
            GroupBy = query.GroupBy,
            Groups = groups,
            OutlierField = query.OutlierField,
            Outliers = outliers
        };
    }

    public DatasetFacts GetDatasetFacts()
    {
        using var connection = _datasetStore.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                COUNT(*),
                SUM(CASE WHEN ConflictStatus = 'usable' THEN 1 ELSE 0 END),
                SUM(CASE WHEN ConflictStatus = 'conflicting' THEN 1 ELSE 0 END)
            FROM Deals;
            """;
        using var reader = command.ExecuteReader();
        reader.Read();
        var dealCount = reader.GetInt32(0);
        var usableDealCount = reader.GetInt32(1);
        var conflictingDealCount = reader.GetInt32(2);
        reader.Dispose();

        return new DatasetFacts
        {
            DealCount = dealCount,
            UsableDealCount = usableDealCount,
            ConflictingDealCount = conflictingDealCount,
            Cities = ReadFilterValues(connection, "City"),
            Neighborhoods = ReadFilterValues(connection, "Neighborhood"),
            PropertyTypes = ReadFilterValues(connection, "PropertyType")
        };
    }

    public DealDetail? GetDeal(string dealId)
    {
        using var connection = _datasetStore.OpenConnection();
        using var dealCommand = connection.CreateCommand();
        dealCommand.CommandText = """
            SELECT DealId, ConflictStatus, ReportCount, DistinctReportCount, CanonicalReportId
            FROM Deals
            WHERE DealId = $dealId;
            """;
        dealCommand.Parameters.AddWithValue("$dealId", dealId);
        using var dealReader = dealCommand.ExecuteReader();
        if (!dealReader.Read())
        {
            return null;
        }

        var detail = new DealDetail
        {
            DealId = dealReader.GetString(0),
            ConflictStatus = dealReader.GetString(1),
            ReportCount = dealReader.GetInt32(2),
            DistinctReportCount = dealReader.GetInt32(3),
            CanonicalReportId = dealReader.IsDBNull(4) ? null : dealReader.GetInt64(4)
        };
        dealReader.Dispose();

        using var reportCommand = connection.CreateCommand();
        reportCommand.CommandText = """
            SELECT Id, SourceRowNumber, RawJson, NormalizedJson, QualityFlagsJson
            FROM Reports
            WHERE DealId = $dealId
            ORDER BY SourceRowNumber;
            """;
        reportCommand.Parameters.AddWithValue("$dealId", dealId);
        using var reportReader = reportCommand.ExecuteReader();
        var reports = new List<DealReportDetail>();
        while (reportReader.Read())
        {
            reports.Add(new DealReportDetail
            {
                ReportId = reportReader.GetInt64(0),
                SourceRowNumber = reportReader.GetInt32(1),
                RawJson = reportReader.GetString(2),
                NormalizedJson = reportReader.GetString(3),
                QualityFlagsJson = reportReader.GetString(4)
            });
        }

        return new DealDetail
        {
            DealId = detail.DealId,
            ConflictStatus = detail.ConflictStatus,
            ReportCount = detail.ReportCount,
            DistinctReportCount = detail.DistinctReportCount,
            CanonicalReportId = detail.CanonicalReportId,
            Reports = reports
        };
    }

    private static string BuildFilteredCte()
    {
        return $"""
            Filtered AS (
                SELECT DealId, City, Neighborhood, PropertyType, Condition, Source, PriceNis, SizeSqm, Rooms, Floor, YearBuilt, SuppliedPricePerSqm
                FROM Deals
                WHERE {FilterPredicate}
            )
            """;
    }

    private static (decimal? Value, string? DealId) ComputeRequestedMetric(SqliteConnection connection, DealFilters filters, QueryMetric metric)
    {
        var (expression, condition, aggregation) = DescribeMetric(metric);
        using var command = connection.CreateCommand();
        command.CommandText = aggregation switch
        {
            MetricAggregation.Min => $"""
                WITH {BuildFilteredCte()}
                SELECT DealId, {expression} AS MetricValue
                FROM Filtered
                WHERE {condition}
                ORDER BY MetricValue ASC
                LIMIT 1;
                """,
            MetricAggregation.Max => $"""
                WITH {BuildFilteredCte()}
                SELECT DealId, {expression} AS MetricValue
                FROM Filtered
                WHERE {condition}
                ORDER BY MetricValue DESC
                LIMIT 1;
                """,
            MetricAggregation.Average => $"""
                WITH {BuildFilteredCte()}
                SELECT NULL, AVG({expression})
                FROM Filtered
                WHERE {condition};
                """,
            MetricAggregation.Median => $"""
                WITH {BuildFilteredCte()},
                Ranked AS (
                    SELECT {expression} AS Value, ROW_NUMBER() OVER (ORDER BY {expression}) AS Position, COUNT(*) OVER () AS Total
                    FROM Filtered
                    WHERE {condition}
                )
                SELECT NULL, AVG(Value)
                FROM Ranked
                WHERE Position IN ((Total + 1) / 2, (Total + 2) / 2);
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unsupported metric aggregation.")
        };
        AddFilterParameters(command, filters);

        using var reader = command.ExecuteReader();
        if (!reader.Read() || reader.IsDBNull(1))
        {
            return (null, null);
        }

        var dealId = reader.IsDBNull(0) ? null : reader.GetString(0);
        var value = Convert.ToDecimal(reader.GetDouble(1));
        return (value, dealId);
    }

    private static (decimal? Value, string? DealId) ComputeRankedMetric(SqliteConnection connection, DealFilters filters, QueryMetric metric, int rank)
    {
        var (expression, condition, aggregation) = DescribeMetric(metric);
        if (aggregation is not (MetricAggregation.Min or MetricAggregation.Max))
        {
            throw new ArgumentOutOfRangeException(nameof(metric), metric, "Only Min/Max metrics can be ranked.");
        }

        var orderDirection = aggregation == MetricAggregation.Min ? "ASC" : "DESC";
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            WITH {BuildFilteredCte()}
            SELECT DealId, {expression} AS MetricValue
            FROM Filtered
            WHERE {condition}
            ORDER BY MetricValue {orderDirection}
            LIMIT 1 OFFSET $offset;
            """;
        AddFilterParameters(command, filters);
        command.Parameters.AddWithValue("$offset", rank - 1);

        using var reader = command.ExecuteReader();
        if (!reader.Read() || reader.IsDBNull(1))
        {
            return (null, null);
        }

        var dealId = reader.IsDBNull(0) ? null : reader.GetString(0);
        var value = Convert.ToDecimal(reader.GetDouble(1));
        return (value, dealId);
    }

    private const int MaxGroups = 200;

    private static IReadOnlyList<GroupedQueryResult> ComputeGroups(
        SqliteConnection connection,
        DealFilters filters,
        GroupByField groupBy,
        IReadOnlyList<QueryMetric> metrics,
        IReadOnlyList<RankedMetricRequest> rankedMetrics)
    {
        var column = DescribeGroupByColumn(groupBy);
        var groups = new Dictionary<string, GroupBuilder>();
        var order = new List<string>();

        GroupBuilder EnsureGroup(string key)
        {
            if (!groups.TryGetValue(key, out var builder))
            {
                builder = new GroupBuilder { GroupValue = key };
                groups[key] = builder;
                order.Add(key);
            }

            return builder;
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"""
                WITH {BuildFilteredCte()}
                SELECT {column} AS GroupValue, COUNT(*) AS Cnt
                FROM Filtered
                WHERE {column} IS NOT NULL
                GROUP BY {column}
                ORDER BY {column}
                LIMIT {MaxGroups};
                """;
            AddFilterParameters(command, filters);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var key = Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? "";
                EnsureGroup(key).TransactionCount = reader.GetInt32(1);
            }
        }

        foreach (var metric in metrics.Distinct())
        {
            if (metric == QueryMetric.TransactionCount)
            {
                continue;
            }

            foreach (var (key, value, dealId) in ComputeMetricByGroup(connection, filters, column, metric, rank: 1))
            {
                EnsureGroup(key).RequestedMetrics.Add(new RequestedMetricResult { Metric = metric, Value = value, DealId = dealId });
            }
        }

        foreach (var request in rankedMetrics)
        {
            foreach (var (key, value, dealId) in ComputeMetricByGroup(connection, filters, column, request.Metric, request.Rank))
            {
                EnsureGroup(key).RankedMetrics.Add(new RankedMetricResult { Metric = request.Metric, Rank = request.Rank, Value = value, DealId = dealId });
            }
        }

        return order.Select(key => groups[key].Build()).ToList();
    }

    private static List<(string GroupValue, decimal? Value, string? DealId)> ComputeMetricByGroup(
        SqliteConnection connection, DealFilters filters, string groupColumn, QueryMetric metric, int rank)
    {
        var (expression, condition, aggregation) = DescribeMetric(metric);
        using var command = connection.CreateCommand();

        if (aggregation is MetricAggregation.Min or MetricAggregation.Max)
        {
            var orderDirection = aggregation == MetricAggregation.Min ? "ASC" : "DESC";
            command.CommandText = $"""
                WITH {BuildFilteredCte()},
                RankedInGroup AS (
                    SELECT DealId, {groupColumn} AS GroupValue, {expression} AS MetricValue,
                           ROW_NUMBER() OVER (PARTITION BY {groupColumn} ORDER BY {expression} {orderDirection}) AS Position
                    FROM Filtered
                    WHERE {condition} AND {groupColumn} IS NOT NULL
                )
                SELECT GroupValue, DealId, MetricValue
                FROM RankedInGroup
                WHERE Position = $rank
                ORDER BY GroupValue
                LIMIT {MaxGroups};
                """;
            command.Parameters.AddWithValue("$rank", rank);
        }
        else if (aggregation == MetricAggregation.Average)
        {
            command.CommandText = $"""
                WITH {BuildFilteredCte()}
                SELECT {groupColumn} AS GroupValue, NULL AS DealId, AVG({expression}) AS MetricValue
                FROM Filtered
                WHERE {condition} AND {groupColumn} IS NOT NULL
                GROUP BY {groupColumn}
                ORDER BY {groupColumn}
                LIMIT {MaxGroups};
                """;
        }
        else
        {
            command.CommandText = $"""
                WITH {BuildFilteredCte()},
                Ranked AS (
                    SELECT {groupColumn} AS GroupValue, {expression} AS Value,
                           ROW_NUMBER() OVER (PARTITION BY {groupColumn} ORDER BY {expression}) AS Position,
                           COUNT(*) OVER (PARTITION BY {groupColumn}) AS Total
                    FROM Filtered
                    WHERE {condition} AND {groupColumn} IS NOT NULL
                )
                SELECT GroupValue, NULL AS DealId, AVG(Value) AS MetricValue
                FROM Ranked
                WHERE Position IN ((Total + 1) / 2, (Total + 2) / 2)
                GROUP BY GroupValue
                ORDER BY GroupValue
                LIMIT {MaxGroups};
                """;
        }

        AddFilterParameters(command, filters);
        using var reader = command.ExecuteReader();
        var results = new List<(string, decimal?, string?)>();
        while (reader.Read())
        {
            var key = Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? "";
            var dealId = reader.IsDBNull(1) ? null : reader.GetString(1);
            var value = reader.IsDBNull(2) ? (decimal?)null : Convert.ToDecimal(reader.GetDouble(2));
            results.Add((key, value, dealId));
        }

        return results;
    }

    private const int MaxOutliers = 200;

    /// <summary>
    /// Flags deals whose value for a field falls outside Q1 - 1.5*IQR .. Q3 + 1.5*IQR (the
    /// standard Tukey fence), computed within each group (or the whole filtered sample when
    /// groupBy is null) using the same nearest-rank percentile technique already used for the
    /// median. This is a distribution-agnostic outlier rule, not a test of normality -- it never
    /// claims to determine whether the data follows a normal distribution.
    /// </summary>
    private static List<OutlierResult> ComputeOutliers(SqliteConnection connection, DealFilters filters, OutlierField field, GroupByField? groupBy)
    {
        var (expression, condition) = DescribeOutlierField(field);
        var partitionColumn = groupBy is { } groupByField ? DescribeGroupByColumn(groupByField) : null;
        var groupValueSelect = partitionColumn is not null ? partitionColumn : "NULL";
        var groupFilter = partitionColumn is not null ? $"AND {partitionColumn} IS NOT NULL" : "";
        const string partitionClause = "PARTITION BY GroupValue";

        using var command = connection.CreateCommand();
        command.CommandText = $"""
            WITH {BuildFilteredCte()},
            Measured AS (
                SELECT DealId, {groupValueSelect} AS GroupValue, {expression} AS Value
                FROM Filtered
                WHERE {condition} {groupFilter}
            ),
            Ranked AS (
                SELECT DealId, GroupValue, Value,
                       ROW_NUMBER() OVER ({partitionClause} ORDER BY Value) AS Position,
                       COUNT(*) OVER ({partitionClause}) AS Total
                FROM Measured
            ),
            Quartiles AS (
                SELECT GroupValue,
                       MAX(CASE WHEN Position = CAST(ROUND(0.25 * Total) AS INTEGER) THEN Value END) AS Q1,
                       MAX(CASE WHEN Position = CAST(ROUND(0.75 * Total) AS INTEGER) THEN Value END) AS Q3
                FROM Ranked
                GROUP BY GroupValue
            )
            SELECT r.GroupValue, r.DealId, r.Value,
                   q.Q1 - 1.5 * (q.Q3 - q.Q1) AS LowerBound,
                   q.Q3 + 1.5 * (q.Q3 - q.Q1) AS UpperBound
            FROM Ranked r
            JOIN Quartiles q ON r.GroupValue IS q.GroupValue
            WHERE q.Q1 IS NOT NULL AND q.Q3 IS NOT NULL
              AND (r.Value < q.Q1 - 1.5 * (q.Q3 - q.Q1) OR r.Value > q.Q3 + 1.5 * (q.Q3 - q.Q1))
            ORDER BY r.GroupValue, r.Value
            LIMIT {MaxOutliers};
            """;
        AddFilterParameters(command, filters);

        using var reader = command.ExecuteReader();
        var results = new List<OutlierResult>();
        while (reader.Read())
        {
            results.Add(new OutlierResult
            {
                GroupValue = reader.IsDBNull(0) ? null : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture),
                DealId = reader.GetString(1),
                Value = Convert.ToDecimal(reader.GetDouble(2)),
                LowerBound = Convert.ToDecimal(reader.GetDouble(3)),
                UpperBound = Convert.ToDecimal(reader.GetDouble(4))
            });
        }

        return results;
    }

    private static (string Expression, string Condition) DescribeOutlierField(OutlierField field)
    {
        var metric = field switch
        {
            OutlierField.Price => QueryMetric.AveragePrice,
            OutlierField.PricePerSqm => QueryMetric.AveragePricePerSqm,
            OutlierField.SizeSqm => QueryMetric.AverageSizeSqm,
            OutlierField.Rooms => QueryMetric.AverageRooms,
            OutlierField.Floor => QueryMetric.AverageFloor,
            OutlierField.YearBuilt => QueryMetric.AverageYearBuilt,
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unsupported outlier field.")
        };

        var (expression, condition, _) = DescribeMetric(metric);
        return (expression, condition);
    }

    private static string DescribeGroupByColumn(GroupByField field)
    {
        return field switch
        {
            GroupByField.City => "City",
            GroupByField.Neighborhood => "Neighborhood",
            GroupByField.PropertyType => "PropertyType",
            GroupByField.Condition => "Condition",
            GroupByField.Source => "Source",
            GroupByField.Rooms => "Rooms",
            GroupByField.Floor => "Floor",
            GroupByField.YearBuilt => "YearBuilt",
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unsupported group-by field.")
        };
    }

    private class GroupBuilder
    {
        public string GroupValue { get; init; } = string.Empty;
        public int TransactionCount { get; set; }
        public List<RequestedMetricResult> RequestedMetrics { get; } = [];
        public List<RankedMetricResult> RankedMetrics { get; } = [];

        public GroupedQueryResult Build()
        {
            return new GroupedQueryResult
            {
                GroupValue = GroupValue,
                TransactionCount = TransactionCount,
                RequestedMetrics = RequestedMetrics,
                RankedMetrics = RankedMetrics
            };
        }
    }

    private enum MetricAggregation { Min, Max, Average, Median }

    private static (string Expression, string Condition, MetricAggregation Aggregation) DescribeMetric(QueryMetric metric)
    {
        return metric switch
        {
            QueryMetric.MinPrice => ("PriceNis", "PriceNis > 0", MetricAggregation.Min),
            QueryMetric.MaxPrice => ("PriceNis", "PriceNis > 0", MetricAggregation.Max),
            QueryMetric.AveragePrice => ("PriceNis", "PriceNis > 0", MetricAggregation.Average),
            QueryMetric.MedianPrice => ("PriceNis", "PriceNis > 0", MetricAggregation.Median),
            QueryMetric.MinPricePerSqm => ("(PriceNis * 1.0 / SizeSqm)", "PriceNis > 0 AND SizeSqm > 0", MetricAggregation.Min),
            QueryMetric.MaxPricePerSqm => ("(PriceNis * 1.0 / SizeSqm)", "PriceNis > 0 AND SizeSqm > 0", MetricAggregation.Max),
            QueryMetric.AveragePricePerSqm => ("(PriceNis * 1.0 / SizeSqm)", "PriceNis > 0 AND SizeSqm > 0", MetricAggregation.Average),
            QueryMetric.MedianPricePerSqm => ("(PriceNis * 1.0 / SizeSqm)", "PriceNis > 0 AND SizeSqm > 0", MetricAggregation.Median),
            QueryMetric.MinSizeSqm => ("SizeSqm", "SizeSqm IS NOT NULL", MetricAggregation.Min),
            QueryMetric.MaxSizeSqm => ("SizeSqm", "SizeSqm IS NOT NULL", MetricAggregation.Max),
            QueryMetric.AverageSizeSqm => ("SizeSqm", "SizeSqm IS NOT NULL", MetricAggregation.Average),
            QueryMetric.MedianSizeSqm => ("SizeSqm", "SizeSqm IS NOT NULL", MetricAggregation.Median),
            QueryMetric.MinRooms => ("Rooms", "Rooms IS NOT NULL", MetricAggregation.Min),
            QueryMetric.MaxRooms => ("Rooms", "Rooms IS NOT NULL", MetricAggregation.Max),
            QueryMetric.AverageRooms => ("Rooms", "Rooms IS NOT NULL", MetricAggregation.Average),
            QueryMetric.MedianRooms => ("Rooms", "Rooms IS NOT NULL", MetricAggregation.Median),
            QueryMetric.MinFloor => ("Floor", "Floor IS NOT NULL", MetricAggregation.Min),
            QueryMetric.MaxFloor => ("Floor", "Floor IS NOT NULL", MetricAggregation.Max),
            QueryMetric.AverageFloor => ("Floor", "Floor IS NOT NULL", MetricAggregation.Average),
            QueryMetric.MedianFloor => ("Floor", "Floor IS NOT NULL", MetricAggregation.Median),
            QueryMetric.MinYearBuilt => ("YearBuilt", "YearBuilt IS NOT NULL", MetricAggregation.Min),
            QueryMetric.MaxYearBuilt => ("YearBuilt", "YearBuilt IS NOT NULL", MetricAggregation.Max),
            QueryMetric.AverageYearBuilt => ("YearBuilt", "YearBuilt IS NOT NULL", MetricAggregation.Average),
            QueryMetric.MedianYearBuilt => ("YearBuilt", "YearBuilt IS NOT NULL", MetricAggregation.Median),
            QueryMetric.TransactionCount => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Transaction count does not need a metric query."),
            _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unsupported metric.")
        };
    }

    private static (IReadOnlyList<string> DealIds, bool HasMore) ReadEvidence(SqliteConnection connection, DealFilters filters, int evidencePageSize)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT DealId
            FROM Deals
            WHERE {FilterPredicate}
            ORDER BY DealId
            LIMIT $limit;
            """;
        AddFilterParameters(command, filters);
        command.Parameters.AddWithValue("$limit", evidencePageSize + 1);
        using var reader = command.ExecuteReader();
        var dealIds = new List<string>();
        while (reader.Read())
        {
            dealIds.Add(reader.GetString(0));
        }

        var hasMore = dealIds.Count > evidencePageSize;
        return (dealIds.Take(evidencePageSize).ToList(), hasMore);
    }

    private static void AddFilterParameters(SqliteCommand command, DealFilters filters)
    {
        command.Parameters.AddWithValue("$city", (object?)filters.City?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("$neighborhood", (object?)filters.Neighborhood?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("$propertyType", (object?)filters.PropertyType?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("$minimumRooms", (object?)filters.MinimumRooms ?? DBNull.Value);
        command.Parameters.AddWithValue("$maximumRooms", (object?)filters.MaximumRooms ?? DBNull.Value);
        command.Parameters.AddWithValue("$startDate", filters.StartDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$endDate", filters.EndDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$minimumFloor", (object?)filters.MinimumFloor ?? DBNull.Value);
        command.Parameters.AddWithValue("$maximumFloor", (object?)filters.MaximumFloor ?? DBNull.Value);
        command.Parameters.AddWithValue("$minimumYearBuilt", (object?)filters.MinimumYearBuilt ?? DBNull.Value);
        command.Parameters.AddWithValue("$maximumYearBuilt", (object?)filters.MaximumYearBuilt ?? DBNull.Value);
        command.Parameters.AddWithValue("$condition", (object?)filters.Condition?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("$source", (object?)filters.Source?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("$hasElevator", ToSqliteBoolean(filters.HasElevator));
        command.Parameters.AddWithValue("$hasParking", ToSqliteBoolean(filters.HasParking));
        command.Parameters.AddWithValue("$hasBalcony", ToSqliteBoolean(filters.HasBalcony));
        command.Parameters.AddWithValue("$hasSafeRoom", ToSqliteBoolean(filters.HasSafeRoom));
    }

    private static object ToSqliteBoolean(bool? value)
    {
        if (value is null)
        {
            return DBNull.Value;
        }

        return value.Value ? 1 : 0;
    }

    private static IReadOnlyList<string> ReadFilterValues(SqliteConnection connection, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT DISTINCT {columnName}
            FROM Deals
            WHERE ConflictStatus = 'usable' AND {columnName} IS NOT NULL AND {columnName} <> ''
            ORDER BY {columnName};
            """;
        using var reader = command.ExecuteReader();
        var values = new List<string>();
        while (reader.Read())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }
}
