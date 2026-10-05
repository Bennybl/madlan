using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace MadlanExplorer;

public class DatasetStore : IDisposable
{
    private const int EvidencePageSize = 100;
    private const int MinimumMetricContributorCount = 5;
    private static readonly string[] ExpectedHeaders =
    [
        "deal_id", "city", "neighborhood", "street", "property_type", "rooms", "size_sqm",
        "floor", "total_floors", "year_built", "condition", "has_elevator", "has_parking",
        "has_balcony", "has_safe_room", "deal_date", "price_nis", "price_per_sqm", "source"
    ];

    private readonly string _dataFilePath;
    private readonly string _connectionString;
    private readonly IsraeliLocalityCatalog _localityCatalog;
    private SqliteConnection? _keeperConnection;

    public DatasetStore(
        IOptions<DatasetOptions> options,
        IHostEnvironment environment,
        IsraeliLocalityCatalog localityCatalog)
    {
        var settings = options.Value;

        var contentRootDataFilePath = Path.Combine(environment.ContentRootPath, settings.DataFile);
        _dataFilePath = File.Exists(contentRootDataFilePath)
            ? contentRootDataFilePath
            : Path.Combine(AppContext.BaseDirectory, settings.DataFile);
        _connectionString = $"Data Source={settings.DatabaseName};Mode=Memory;Cache=Shared;Pooling=False";
        _localityCatalog = localityCatalog;
    }

    public DatasetMetadata Metadata { get; private set; } = new();

    public void Load()
    {
        if (_keeperConnection is not null)
        {
            return;
        }

        if (!File.Exists(_dataFilePath))
        {
            throw new InvalidOperationException($"Dataset file was not found at '{_dataFilePath}'.");
        }

        var fileBytes = File.ReadAllBytes(_dataFilePath);
        _keeperConnection = new SqliteConnection(_connectionString);
        _keeperConnection.Open();

        try
        {
            CreateSchema(_keeperConnection);
            LoadReports(_keeperConnection, fileBytes);
        }
        catch
        {
            _keeperConnection.Dispose();
            _keeperConnection = null;
            throw;
        }
    }

    public SqliteConnection OpenConnection()
    {
        if (_keeperConnection is null)
        {
            throw new InvalidOperationException("The dataset has not been loaded.");
        }

        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public void Dispose()
    {
        _keeperConnection?.Dispose();
        _keeperConnection = null;
    }

    private static void CreateSchema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE Reports (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SourceRowNumber INTEGER NOT NULL,
                DealId TEXT NOT NULL,
                RawJson TEXT NOT NULL,
                NormalizedJson TEXT NOT NULL,
                QualityFlagsJson TEXT NOT NULL
            );
            CREATE INDEX IX_Reports_DealId ON Reports (DealId);

            CREATE TABLE Deals (
                DealId TEXT PRIMARY KEY,
                ConflictStatus TEXT NOT NULL CHECK (ConflictStatus IN ('usable', 'conflicting')),
                CanonicalReportId INTEGER NULL REFERENCES Reports (Id),
                ReportCount INTEGER NOT NULL,
                DistinctReportCount INTEGER NOT NULL,
                City TEXT NULL,
                Neighborhood TEXT NULL,
                PropertyType TEXT NULL,
                Rooms REAL NULL,
                SizeSqm REAL NULL,
                PriceNis REAL NULL,
                SuppliedPricePerSqm REAL NULL,
                DealDateStart TEXT NULL,
                DealDateEnd TEXT NULL
            );
            CREATE INDEX IX_Deals_Filter ON Deals (ConflictStatus, City, Neighborhood, PropertyType, Rooms, DealDateStart, DealDateEnd);
            """;
        command.ExecuteNonQuery();
    }

    private void LoadReports(SqliteConnection connection, byte[] fileBytes)
    {
        using var transaction = connection.BeginTransaction();
        using var stream = new MemoryStream(fileBytes);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            BadDataFound = null,
            MissingFieldFound = null
        });

        if (!csv.Read())
        {
            throw new InvalidOperationException("Dataset CSV is empty.");
        }

        csv.ReadHeader();
        ValidateHeaders(csv.HeaderRecord);
        var parser = csv.Context.Parser ?? throw new InvalidOperationException("Dataset CSV parser was not initialized.");

        var reportCount = 0;
        while (csv.Read())
        {
            var rawFields = ExpectedHeaders.ToDictionary(header => header, header => csv.GetField(header) ?? string.Empty);
            var normalized = Normalize(rawFields, out var qualityFlags);

            InsertReport(connection, transaction, parser.Row, rawFields, normalized, qualityFlags);
            reportCount++;
        }

        PopulateDeals(connection, transaction);
        transaction.Commit();
        Metadata = new DatasetMetadata
        {
            FileHash = Convert.ToHexString(SHA256.HashData(fileBytes)),
            ReportCount = reportCount
        };
    }

    private static void ValidateHeaders(string[]? headers)
    {
        if (headers is null || headers.Length != ExpectedHeaders.Length)
        {
            throw new InvalidOperationException("Dataset CSV headers do not match the expected schema.");
        }

        var actualHeaders = new HashSet<string>(headers, StringComparer.Ordinal);
        if (actualHeaders.Count != headers.Length || !actualHeaders.SetEquals(ExpectedHeaders))
        {
            throw new InvalidOperationException("Dataset CSV headers do not match the expected schema.");
        }
    }

    private static void InsertReport(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int sourceRowNumber,
        IReadOnlyDictionary<string, string> rawFields,
        NormalizedDealReport normalized,
        IReadOnlyList<string> qualityFlags)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Reports (SourceRowNumber, DealId, RawJson, NormalizedJson, QualityFlagsJson)
            VALUES ($sourceRowNumber, $dealId, $rawJson, $normalizedJson, $qualityFlagsJson);
            """;
        command.Parameters.AddWithValue("$sourceRowNumber", sourceRowNumber);
        command.Parameters.AddWithValue("$dealId", normalized.DealId);
        command.Parameters.AddWithValue("$rawJson", JsonSerializer.Serialize(rawFields));
        command.Parameters.AddWithValue("$normalizedJson", JsonSerializer.Serialize(normalized));
        command.Parameters.AddWithValue("$qualityFlagsJson", JsonSerializer.Serialize(qualityFlags));
        command.ExecuteNonQuery();
    }

    private NormalizedDealReport Normalize(
        IReadOnlyDictionary<string, string> fields,
        out IReadOnlyList<string> qualityFlags)
    {
        var flags = new List<string>();
        var dealDate = NormalizeDate(fields["deal_date"], flags);
        var locality = _localityCatalog.Resolve(NormalizeText(fields["city"]));
        if (locality.Method is "typo" or "ambiguous" or "unresolved") flags.Add($"city_{locality.Method}");

        var normalized = new NormalizedDealReport
        {
            DealId = NormalizeText(fields["deal_id"]),
            City = locality.ResolvedValue,
            Locality = locality,
            Neighborhood = NormalizeNullableText(fields["neighborhood"]),
            Street = NormalizeNullableText(fields["street"]),
            PropertyType = NormalizeText(fields["property_type"]),
            Rooms = NormalizeDecimal(fields["rooms"], "rooms", flags),
            SizeSqm = NormalizeDecimal(fields["size_sqm"], "size_sqm", flags),
            Floor = NormalizeDecimal(fields["floor"], "floor", flags),
            TotalFloors = NormalizeDecimal(fields["total_floors"], "total_floors", flags),
            YearBuilt = NormalizeDecimal(fields["year_built"], "year_built", flags),
            Condition = NormalizeNullableText(fields["condition"]),
            PriceNis = NormalizeDecimal(fields["price_nis"], "price_nis", flags),
            SuppliedPricePerSqm = NormalizeDecimal(fields["price_per_sqm"], "price_per_sqm", flags),
            HasElevator = NormalizeBoolean(fields["has_elevator"], "has_elevator", flags),
            HasParking = NormalizeBoolean(fields["has_parking"], "has_parking", flags),
            HasBalcony = NormalizeBoolean(fields["has_balcony"], "has_balcony", flags),
            HasSafeRoom = NormalizeBoolean(fields["has_safe_room"], "has_safe_room", flags),
            DealDateStart = dealDate.Start,
            DealDateEnd = dealDate.End,
            DealDatePrecision = dealDate.Precision,
            Source = NormalizeText(fields["source"])
        };

        qualityFlags = flags;
        return normalized;
    }

    private static void PopulateDeals(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Deals (
                DealId,
                ConflictStatus,
                CanonicalReportId,
                ReportCount,
                DistinctReportCount)
            SELECT
                DealId,
                CASE WHEN COUNT(DISTINCT NormalizedJson) = 1 THEN 'usable' ELSE 'conflicting' END,
                CASE WHEN COUNT(DISTINCT NormalizedJson) = 1 THEN MIN(Id) ELSE NULL END,
                COUNT(*),
                COUNT(DISTINCT NormalizedJson)
            FROM Reports
            GROUP BY DealId;

            UPDATE Deals
            SET
                City = (SELECT json_extract(NormalizedJson, '$.City') FROM Reports WHERE Id = Deals.CanonicalReportId),
                Neighborhood = (SELECT json_extract(NormalizedJson, '$.Neighborhood') FROM Reports WHERE Id = Deals.CanonicalReportId),
                PropertyType = (SELECT json_extract(NormalizedJson, '$.PropertyType') FROM Reports WHERE Id = Deals.CanonicalReportId),
                Rooms = (SELECT json_extract(NormalizedJson, '$.Rooms') FROM Reports WHERE Id = Deals.CanonicalReportId),
                SizeSqm = (SELECT json_extract(NormalizedJson, '$.SizeSqm') FROM Reports WHERE Id = Deals.CanonicalReportId),
                PriceNis = (SELECT json_extract(NormalizedJson, '$.PriceNis') FROM Reports WHERE Id = Deals.CanonicalReportId),
                SuppliedPricePerSqm = (SELECT json_extract(NormalizedJson, '$.SuppliedPricePerSqm') FROM Reports WHERE Id = Deals.CanonicalReportId),
                DealDateStart = (SELECT json_extract(NormalizedJson, '$.DealDateStart') FROM Reports WHERE Id = Deals.CanonicalReportId),
                DealDateEnd = (SELECT json_extract(NormalizedJson, '$.DealDateEnd') FROM Reports WHERE Id = Deals.CanonicalReportId)
            WHERE CanonicalReportId IS NOT NULL;
            """;
        command.ExecuteNonQuery();
    }

    public DealQueryResult ExecuteQuery(DealFilters filters)
    {
        ValidateFilters(filters);
        using var connection = OpenConnection();
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
        var evidence = ReadEvidence(connection, filters);
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

    private static (IReadOnlyList<string> DealIds, bool HasMore) ReadEvidence(SqliteConnection connection, DealFilters filters)
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
        command.Parameters.AddWithValue("$limit", EvidencePageSize + 1);
        using var reader = command.ExecuteReader();
        var dealIds = new List<string>();
        while (reader.Read()) dealIds.Add(reader.GetString(0));
        var hasMore = dealIds.Count > EvidencePageSize;
        return (dealIds.Take(EvidencePageSize).ToList(), hasMore);
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

    private static void ValidateFilters(DealFilters filters)
    {
        if (filters.MinimumRooms is < 0 || filters.MaximumRooms is < 0 || filters.MinimumRooms > filters.MaximumRooms) throw new ArgumentException("Room bounds are invalid.", nameof(filters));
        if (filters.StartDate > filters.EndDate) throw new ArgumentException("Date bounds are invalid.", nameof(filters));
    }

    private static string NormalizeText(string value) => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string? NormalizeNullableText(string value)
    {
        var normalized = NormalizeText(value);
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private static decimal? NormalizeDecimal(string value, string field, ICollection<string> flags)
    {
        var normalized = NormalizeText(value).Replace("₪", string.Empty, StringComparison.Ordinal).Replace(",", string.Empty, StringComparison.Ordinal);
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        normalized = normalized.Replace("חדרים", string.Empty, StringComparison.Ordinal).Trim();
        if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        flags.Add($"{field}_unrecognized");
        return null;
    }

    private static bool? NormalizeBoolean(string value, string field, ICollection<string> flags)
    {
        var normalized = NormalizeText(value).ToLowerInvariant();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized is "true" or "yes" or "1" or "כן")
        {
            return true;
        }

        if (normalized is "false" or "no" or "0" or "לא")
        {
            return false;
        }

        flags.Add($"{field}_unrecognized");
        return null;
    }

    private static NormalizedDate NormalizeDate(string value, ICollection<string> flags)
    {
        var normalized = NormalizeText(value);
        if (DateOnly.TryParseExact(normalized, ["yyyy-MM-dd", "dd.MM.yyyy", "dd/MM/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            return new NormalizedDate(day, day, "day");
        }

        if (DateTime.TryParseExact(normalized, "MMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var month))
        {
            var start = new DateOnly(month.Year, month.Month, 1);
            return new NormalizedDate(start, start.AddMonths(1).AddDays(-1), "month");
        }

        if (!string.IsNullOrEmpty(normalized))
        {
            flags.Add("deal_date_unrecognized");
        }

        return new NormalizedDate(null, null, "unknown");
    }

}
