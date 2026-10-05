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

        decimal? requestedMetricValue = null;
        string? requestedMetricDealId = null;
        if (query.Metric != QueryMetric.TransactionCount)
        {
            (requestedMetricValue, requestedMetricDealId) = ComputeRequestedMetric(connection, filters, query.Metric);
        }

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
            RequestedMetric = query.Metric == QueryMetric.TransactionCount ? null : query.Metric,
            RequestedMetricValue = requestedMetricValue,
            RequestedMetricDealId = requestedMetricDealId
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
                SELECT DealId, PriceNis, SizeSqm, Rooms, Floor, YearBuilt, SuppliedPricePerSqm
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
