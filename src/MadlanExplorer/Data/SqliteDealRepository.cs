using System.Globalization;
using Microsoft.Data.Sqlite;

namespace MadlanExplorer;

public class SqliteDealRepository : IDealRepository
{
    private const int MinimumMetricContributorCount = 5;
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
        command.CommandText = """
            WITH Filtered AS (
                SELECT DealId, PriceNis, SizeSqm, SuppliedPricePerSqm
                FROM Deals
                WHERE ConflictStatus = 'usable'
                  AND ($city IS NULL OR City = $city)
                  AND ($neighborhood IS NULL OR Neighborhood = $neighborhood)
                  AND ($propertyType IS NULL OR PropertyType = $propertyType)
                  AND ($minimumRooms IS NULL OR Rooms >= $minimumRooms)
                  AND ($maximumRooms IS NULL OR Rooms <= $maximumRooms)
                  AND ($startDate IS NULL OR DealDateStart >= $startDate)
                  AND ($endDate IS NULL OR DealDateEnd <= $endDate)
            ),
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
                EXISTS (SELECT 1 FROM Filtered WHERE PriceNis > 0 AND SizeSqm > 0 AND SuppliedPricePerSqm IS NOT NULL AND ABS(SuppliedPricePerSqm - (PriceNis / SizeSqm)) > MAX(1, (PriceNis / SizeSqm) * 0.01));
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
        if (reader.GetInt64(5) == 1) warnings.Add("low_price_reported");
        if (priceContributorCount < MinimumMetricContributorCount) warnings.Add("price_metric_has_fewer_than_five_contributors");
        if (pricePerSqmContributorCount < MinimumMetricContributorCount) warnings.Add("price_per_sqm_metric_has_fewer_than_five_contributors");
        if (reader.GetInt64(6) == 1) warnings.Add("supplied_price_per_sqm_mismatch");
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

    private static (IReadOnlyList<string> DealIds, bool HasMore) ReadEvidence(SqliteConnection connection, DealFilters filters, int evidencePageSize)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DealId
            FROM Deals
            WHERE ConflictStatus = 'usable'
              AND ($city IS NULL OR City = $city)
              AND ($neighborhood IS NULL OR Neighborhood = $neighborhood)
              AND ($propertyType IS NULL OR PropertyType = $propertyType)
              AND ($minimumRooms IS NULL OR Rooms >= $minimumRooms)
              AND ($maximumRooms IS NULL OR Rooms <= $maximumRooms)
              AND ($startDate IS NULL OR DealDateStart >= $startDate)
              AND ($endDate IS NULL OR DealDateEnd <= $endDate)
            ORDER BY DealId
            LIMIT $limit;
            """;
        AddFilterParameters(command, filters);
        command.Parameters.AddWithValue("$limit", evidencePageSize + 1);
        using var reader = command.ExecuteReader();
        var dealIds = new List<string>();
        while (reader.Read()) dealIds.Add(reader.GetString(0));
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


