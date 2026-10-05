using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MadlanExplorer;
using Xunit;

namespace MadlanExplorer.Tests;

public class DatasetStoreTests
{
    [Fact]
    public void Load_populates_all_sample_rows_and_preserves_report_data()
    {
        using var store = CreateStore();

        store.Load();

        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Reports;";

        Assert.Equal(530L, command.ExecuteScalar());
        Assert.Equal(530, store.Metadata.ReportCount);
        Assert.NotEmpty(store.Metadata.FileHash);
    }

    [Fact]
    public void Load_normalizes_known_formats_and_keeps_raw_values()
    {
        using var store = CreateStore();
        store.Load();

        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT RawJson, NormalizedJson FROM Reports WHERE DealId = 'D100477';";
        using var reader = command.ExecuteReader();

        Assert.True(reader.Read());
        var rawJson = reader.GetString(0);
        var normalized = JsonSerializer.Deserialize<NormalizedDealReport>(reader.GetString(1));

        Assert.Contains("4,331,000", rawJson);
        Assert.NotNull(normalized);
        Assert.Equal("בית שמש", normalized.City);
        Assert.Equal("בית-שמש", normalized.Locality.OriginalValue);
        Assert.Equal("typo", normalized.Locality.Method);
        Assert.Equal(5.5m, normalized.Rooms);
        Assert.Equal(4_331_000m, normalized.PriceNis);
        Assert.Equal(new DateOnly(2024, 4, 9), normalized.DealDateStart);
        Assert.Equal("day", normalized.DealDatePrecision);
    }

    [Fact]
    public void Load_preserves_month_only_date_precision()
    {
        using var store = CreateStore();
        store.Load();

        var normalized = ReadNormalizedReport(store, "D100171");

        Assert.Equal(new DateOnly(2025, 8, 1), normalized.DealDateStart);
        Assert.Equal(new DateOnly(2025, 8, 31), normalized.DealDateEnd);
        Assert.Equal("month", normalized.DealDatePrecision);
    }

    [Fact]
    public void Keeper_connection_keeps_database_available_to_multiple_request_connections()
    {
        using var store = CreateStore();
        store.Load();

        using (var firstConnection = store.OpenConnection())
        {
            Assert.Equal(530L, CountReports(firstConnection));
        }

        using var secondConnection = store.OpenConnection();
        Assert.Equal(530L, CountReports(secondConnection));
    }

    [Fact]
    public void Load_groups_duplicate_and_conflicting_reports_without_losing_evidence()
    {
        using var store = CreateStore();
        store.Load();

        using var connection = store.OpenConnection();

        Assert.Equal(520L, ExecuteCount(connection, "SELECT COUNT(*) FROM Deals;"));
        Assert.Equal(516L, ExecuteCount(connection, "SELECT COUNT(*) FROM Deals WHERE ConflictStatus = 'usable';"));
        Assert.Equal(4L, ExecuteCount(connection, "SELECT COUNT(*) FROM Deals WHERE ConflictStatus = 'conflicting';"));
        Assert.Equal(6L, ExecuteCount(connection, "SELECT COUNT(*) FROM Deals WHERE ReportCount = 2 AND DistinctReportCount = 1;"));
        Assert.Equal(0L, ExecuteCount(connection, "SELECT COUNT(*) FROM Deals WHERE ConflictStatus = 'conflicting' AND CanonicalReportId IS NOT NULL;"));
        Assert.Equal(6L, ExecuteCount(connection, "SELECT COUNT(*) FROM Deals WHERE ConflictStatus = 'usable' AND ReportCount = 2 AND CanonicalReportId IS NOT NULL;"));

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT DealId FROM Deals WHERE ConflictStatus = 'conflicting' ORDER BY DealId;";
        using var reader = command.ExecuteReader();
        var conflictingDealIds = new List<string>();
        while (reader.Read())
        {
            conflictingDealIds.Add(reader.GetString(0));
        }

        Assert.Equal(["D100017", "D100032", "D100124", "D100303"], conflictingDealIds);
    }

    [Fact]
    public void Load_produces_the_same_deal_outcomes_when_rows_are_reversed()
    {
        var temporaryCsvPath = Path.Combine(Path.GetTempPath(), $"madlan-deals-{Guid.NewGuid():N}.csv");
        var rows = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "data", "madlan_deals_sample.csv"));
        File.WriteAllLines(temporaryCsvPath, [rows[0], .. rows.Skip(1).Reverse()]);

        try
        {
            using var originalStore = CreateStore();
            using var reversedStore = CreateStore(temporaryCsvPath);
            originalStore.Load();
            reversedStore.Load();

            Assert.Equal(ReadDealOutcomes(originalStore), ReadDealOutcomes(reversedStore));
        }
        finally
        {
            File.Delete(temporaryCsvPath);
        }
    }

    private static DatasetStore CreateStore(string? dataFilePath = null)
    {
        var dataFile = dataFilePath ?? Path.Combine(AppContext.BaseDirectory, "data", "madlan_deals_sample.csv");
        var environment = new TestHostEnvironment(AppContext.BaseDirectory);
        var options = Options.Create(new DatasetOptions
        {
            DataFile = Path.GetRelativePath(environment.ContentRootPath, dataFile),
            DatabaseName = $"MadlanTests-{Guid.NewGuid():N}"
        });

        var localityCatalog = new IsraeliLocalityCatalog(options, environment);
        localityCatalog.Load();

        return new DatasetStore(options, environment, localityCatalog);
    }

    private static long CountReports(Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Reports;";
        return (long)command.ExecuteScalar()!;
    }

    private static long ExecuteCount(Microsoft.Data.Sqlite.SqliteConnection connection, string query)
    {
        using var command = connection.CreateCommand();
        command.CommandText = query;
        return (long)command.ExecuteScalar()!;
    }

    private static List<DealOutcome> ReadDealOutcomes(DatasetStore store)
    {
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DealId, ConflictStatus, ReportCount, DistinctReportCount
            FROM Deals
            ORDER BY DealId;
            """;
        using var reader = command.ExecuteReader();
        var outcomes = new List<DealOutcome>();
        while (reader.Read())
        {
            outcomes.Add(new DealOutcome(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt64(2),
                reader.GetInt64(3)));
        }

        return outcomes;
    }

    private static NormalizedDealReport ReadNormalizedReport(DatasetStore store, string dealId)
    {
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT NormalizedJson FROM Reports WHERE DealId = $dealId;";
        command.Parameters.AddWithValue("$dealId", dealId);

        var normalizedJson = (string)command.ExecuteScalar()!;
        return JsonSerializer.Deserialize<NormalizedDealReport>(normalizedJson)!;
    }

    private class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
        }

        public string EnvironmentName { get; set; } = "Test";

        public string ApplicationName { get; set; } = "MadlanExplorer.Tests";

        public string ContentRootPath { get; set; }

        public IFileProvider ContentRootFileProvider { get; set; }
    }

    private record DealOutcome(string DealId, string ConflictStatus, long ReportCount, long DistinctReportCount);
}
