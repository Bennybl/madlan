using System.Globalization;
using Microsoft.Data.Sqlite;

namespace MadlanExplorer;

public class SqliteDealRepository : IDealRepository
{
    private const int MinimumMetricContributorCount = 5;
    private const int MaxRows = 200;

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
            Warnings = warnings
        };
    }

    /// <summary>
    /// Executes one generic, bounded data query (see DataQuery). Every branch below builds fixed,
    /// parameterized SQL from enum-driven column/expression mappings (DescribeGroupByColumn,
    /// DescribeField) -- the caller (an LLM, via QueryOrchestrationService) only ever picks which
    /// of these fixed shapes to run and with which filter/rank/limit values; it never supplies SQL
    /// text itself.
    /// </summary>
    public DataQueryResult ExecuteDataQuery(DataQuery query)
    {
        using var connection = _datasetStore.OpenConnection();
        var filters = query.Filters;
        var totalCount = ExecuteCountValue(connection, filters);
        var groupColumn = query.GroupBy is { } groupBy ? DescribeGroupByColumn(groupBy) : null;

        List<DataRow> rows;
        switch (query.Aggregate)
        {
            case DataAggregate.Count:
                rows = groupColumn is null
                    ? [new DataRow { Value = totalCount }]
                    : GroupedCount(connection, filters, groupColumn, query.Limit, query.Descending);
                break;

            case DataAggregate.Outliers:
            {
                var field = query.Field ?? throw new ArgumentException("Field is required for Outliers.", nameof(query));
                var (expression, condition) = DescribeField(field);
                rows = Outliers(connection, filters, expression, condition, groupColumn);
                break;
            }

            case DataAggregate.Average:
            case DataAggregate.Median:
            {
                var field = query.Field ?? throw new ArgumentException("Field is required for this aggregate.", nameof(query));
                var (expression, condition) = DescribeField(field);
                rows = groupColumn is null
                    ? AverageOrMedianUngrouped(connection, filters, expression, condition, query.Aggregate)
                    : AverageOrMedianGrouped(connection, filters, groupColumn, expression, condition, query.Aggregate, query.Limit, query.Descending);
                break;
            }

            case DataAggregate.Min:
            case DataAggregate.Max:
            {
                var field = query.Field ?? throw new ArgumentException("Field is required for this aggregate.", nameof(query));
                var (expression, condition) = DescribeField(field);
                rows = groupColumn is null
                    ? MinMaxUngrouped(connection, filters, expression, condition, query.Aggregate, query.Rank)
                    : MinMaxGrouped(connection, filters, groupColumn, expression, condition, query.Aggregate, query.Rank, query.Limit, query.Descending);
                break;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(query), query.Aggregate, "Unsupported aggregate.");
        }

        return new DataQueryResult { TransactionCount = totalCount, Rows = rows };
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
            PropertyTypes = ReadFilterValues(connection, "PropertyType"),
            Conditions = ReadFilterValues(connection, "Condition"),
            Sources = ReadFilterValues(connection, "Source")
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
                SELECT DealId, City, Neighborhood, PropertyType, Condition, Source, PriceNis, SizeSqm, Rooms, Floor, YearBuilt, SuppliedPricePerSqm,
                       HasElevator, HasParking, HasBalcony, HasSafeRoom
                FROM Deals
                WHERE {FilterPredicate}
            )
            """;
    }

    private static int ExecuteCountValue(SqliteConnection connection, DealFilters filters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"WITH {BuildFilteredCte()} SELECT COUNT(*) FROM Filtered;";
        AddFilterParameters(command, filters);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static List<DataRow> GroupedCount(SqliteConnection connection, DealFilters filters, string column, int? limit, bool descending)
    {
        using var command = connection.CreateCommand();
        var orderClause = limit.HasValue ? $"ORDER BY Cnt {(descending ? "DESC" : "ASC")}" : $"ORDER BY {column}";
        var limitClause = $"LIMIT {Math.Clamp(limit ?? MaxRows, 1, MaxRows)}";
        command.CommandText = $"""
            WITH {BuildFilteredCte()}
            SELECT {column} AS GroupValue, COUNT(*) AS Cnt
            FROM Filtered
            WHERE {column} IS NOT NULL
            GROUP BY {column}
            {orderClause}
            {limitClause};
            """;
        AddFilterParameters(command, filters);
        using var reader = command.ExecuteReader();
        var rows = new List<DataRow>();
        while (reader.Read())
        {
            rows.Add(new DataRow
            {
                GroupValue = Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture),
                Value = reader.GetInt32(1)
            });
        }

        return rows;
    }

    private static List<DataRow> AverageOrMedianUngrouped(
        SqliteConnection connection, DealFilters filters, string expression, string condition, DataAggregate aggregate)
    {
        using var command = connection.CreateCommand();
        command.CommandText = aggregate == DataAggregate.Average
            ? $"""
                WITH {BuildFilteredCte()}
                SELECT AVG({expression})
                FROM Filtered
                WHERE {condition};
                """
            : $"""
                WITH {BuildFilteredCte()},
                Ranked AS (
                    SELECT {expression} AS Value, ROW_NUMBER() OVER (ORDER BY {expression}) AS Position, COUNT(*) OVER () AS Total
                    FROM Filtered
                    WHERE {condition}
                )
                SELECT AVG(Value)
                FROM Ranked
                WHERE Position IN ((Total + 1) / 2, (Total + 2) / 2);
                """;
        AddFilterParameters(command, filters);
        var result = command.ExecuteScalar();
        var value = result is null or DBNull ? (decimal?)null : Convert.ToDecimal(result);
        return [new DataRow { Value = value }];
    }

    private static List<DataRow> AverageOrMedianGrouped(
        SqliteConnection connection, DealFilters filters, string column, string expression, string condition,
        DataAggregate aggregate, int? limit, bool descending)
    {
        using var command = connection.CreateCommand();
        var orderClause = limit.HasValue ? $"ORDER BY MetricValue {(descending ? "DESC" : "ASC")}" : "ORDER BY GroupValue";
        var limitClause = $"LIMIT {Math.Clamp(limit ?? MaxRows, 1, MaxRows)}";
        command.CommandText = aggregate == DataAggregate.Average
            ? $"""
                WITH {BuildFilteredCte()}
                SELECT {column} AS GroupValue, AVG({expression}) AS MetricValue
                FROM Filtered
                WHERE {condition} AND {column} IS NOT NULL
                GROUP BY {column}
                {orderClause}
                {limitClause};
                """
            : $"""
                WITH {BuildFilteredCte()},
                Ranked AS (
                    SELECT {column} AS GroupValue, {expression} AS Value,
                           ROW_NUMBER() OVER (PARTITION BY {column} ORDER BY {expression}) AS Position,
                           COUNT(*) OVER (PARTITION BY {column}) AS Total
                    FROM Filtered
                    WHERE {condition} AND {column} IS NOT NULL
                )
                SELECT GroupValue, AVG(Value) AS MetricValue
                FROM Ranked
                WHERE Position IN ((Total + 1) / 2, (Total + 2) / 2)
                GROUP BY GroupValue
                {orderClause}
                {limitClause};
                """;
        AddFilterParameters(command, filters);
        using var reader = command.ExecuteReader();
        var rows = new List<DataRow>();
        while (reader.Read())
        {
            rows.Add(new DataRow
            {
                GroupValue = Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture),
                Value = reader.IsDBNull(1) ? null : Convert.ToDecimal(reader.GetDouble(1))
            });
        }

        return rows;
    }

    private static List<DataRow> MinMaxUngrouped(
        SqliteConnection connection, DealFilters filters, string expression, string condition, DataAggregate aggregate, int rank)
    {
        var direction = aggregate == DataAggregate.Min ? "ASC" : "DESC";
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            WITH {BuildFilteredCte()}
            SELECT DealId, {expression} AS MetricValue
            FROM Filtered
            WHERE {condition}
            ORDER BY MetricValue {direction}
            LIMIT 1 OFFSET $offset;
            """;
        AddFilterParameters(command, filters);
        command.Parameters.AddWithValue("$offset", Math.Max(0, rank - 1));

        using var reader = command.ExecuteReader();
        if (!reader.Read() || reader.IsDBNull(1))
        {
            return [];
        }

        return [new DataRow { DealId = reader.GetString(0), Value = Convert.ToDecimal(reader.GetDouble(1)) }];
    }

    private static List<DataRow> MinMaxGrouped(
        SqliteConnection connection, DealFilters filters, string column, string expression, string condition,
        DataAggregate aggregate, int rank, int? limit, bool descending)
    {
        var direction = aggregate == DataAggregate.Min ? "ASC" : "DESC";
        var outerOrder = limit.HasValue ? $"ORDER BY MetricValue {(descending ? "DESC" : "ASC")}" : "ORDER BY GroupValue";
        var limitClause = $"LIMIT {Math.Clamp(limit ?? MaxRows, 1, MaxRows)}";
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            WITH {BuildFilteredCte()},
            RankedInGroup AS (
                SELECT DealId, {column} AS GroupValue, {expression} AS MetricValue,
                       ROW_NUMBER() OVER (PARTITION BY {column} ORDER BY {expression} {direction}) AS Position
                FROM Filtered
                WHERE {condition} AND {column} IS NOT NULL
            )
            SELECT GroupValue, DealId, MetricValue
            FROM RankedInGroup
            WHERE Position = $rank
            {outerOrder}
            {limitClause};
            """;
        AddFilterParameters(command, filters);
        command.Parameters.AddWithValue("$rank", Math.Max(1, rank));

        using var reader = command.ExecuteReader();
        var rows = new List<DataRow>();
        while (reader.Read())
        {
            rows.Add(new DataRow
            {
                GroupValue = Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture),
                DealId = reader.IsDBNull(1) ? null : reader.GetString(1),
                Value = reader.IsDBNull(2) ? null : Convert.ToDecimal(reader.GetDouble(2))
            });
        }

        return rows;
    }

    /// <summary>
    /// Flags deals whose value for a field falls outside Q1 - 1.5*IQR .. Q3 + 1.5*IQR (the
    /// standard Tukey fence), computed within each group (or the whole filtered sample when
    /// groupColumn is null) using the same nearest-rank percentile technique used for the median.
    /// This is a distribution-agnostic outlier rule, not a test of normality -- it never claims to
    /// determine whether the data follows a normal distribution.
    /// </summary>
    private static List<DataRow> Outliers(SqliteConnection connection, DealFilters filters, string expression, string condition, string? groupColumn)
    {
        var groupValueSelect = groupColumn ?? "NULL";
        var groupFilter = groupColumn is not null ? $"AND {groupColumn} IS NOT NULL" : "";
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
            LIMIT {MaxRows};
            """;
        AddFilterParameters(command, filters);

        using var reader = command.ExecuteReader();
        var rows = new List<DataRow>();
        while (reader.Read())
        {
            rows.Add(new DataRow
            {
                GroupValue = reader.IsDBNull(0) ? null : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture),
                DealId = reader.GetString(1),
                Value = Convert.ToDecimal(reader.GetDouble(2)),
                LowerBound = Convert.ToDecimal(reader.GetDouble(3)),
                UpperBound = Convert.ToDecimal(reader.GetDouble(4))
            });
        }

        return rows;
    }

    private static (string Expression, string Condition) DescribeField(DataField field)
    {
        return field switch
        {
            DataField.Price => ("PriceNis", "PriceNis > 0"),
            DataField.PricePerSqm => ("(PriceNis * 1.0 / SizeSqm)", "PriceNis > 0 AND SizeSqm > 0"),
            DataField.SizeSqm => ("SizeSqm", "SizeSqm IS NOT NULL"),
            DataField.Rooms => ("Rooms", "Rooms IS NOT NULL"),
            DataField.Floor => ("Floor", "Floor IS NOT NULL"),
            DataField.YearBuilt => ("YearBuilt", "YearBuilt IS NOT NULL"),
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unsupported field.")
        };
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
            GroupByField.HasElevator => "HasElevator",
            GroupByField.HasParking => "HasParking",
            GroupByField.HasBalcony => "HasBalcony",
            GroupByField.HasSafeRoom => "HasSafeRoom",
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unsupported group-by field.")
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
