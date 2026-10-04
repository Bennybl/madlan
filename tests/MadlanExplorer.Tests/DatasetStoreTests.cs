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

    private static DatasetStore CreateStore()
    {
        var dataFile = Path.Combine(AppContext.BaseDirectory, "data", "madlan_deals_sample.csv");
        var environment = new TestHostEnvironment(AppContext.BaseDirectory);
        var options = Options.Create(new DatasetOptions
        {
            DataFile = Path.GetRelativePath(environment.ContentRootPath, dataFile),
            DatabaseName = $"MadlanTests-{Guid.NewGuid():N}"
        });

        return new DatasetStore(options, environment);
    }

    private static long CountReports(Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Reports;";
        return (long)command.ExecuteScalar()!;
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
}
