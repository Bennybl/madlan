using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using MadlanExplorer;
using Xunit;

namespace MadlanExplorer.Tests;

public class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Dataset_returns_metadata_quality_summary_and_filter_options()
    {
        var response = await _client.GetAsync("/api/dataset");

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.Contains("X-Request-Id"));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(530, body.RootElement.GetProperty("reportCount").GetInt32());
        Assert.Equal(516, body.RootElement.GetProperty("facts").GetProperty("usableDealCount").GetInt32());
        Assert.NotEmpty(body.RootElement.GetProperty("facts").GetProperty("cities").EnumerateArray());
    }

    [Fact]
    public async Task Query_returns_applied_filters_dataset_hash_and_evidence()
    {
        var response = await _client.PostAsJsonAsync("/api/query", new DealFilters());

        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("datasetHash").GetString()));
        Assert.Equal(516, body.RootElement.GetProperty("result").GetProperty("transactionCount").GetInt32());
        Assert.True(body.RootElement.GetProperty("result").GetProperty("hasMoreEvidence").GetBoolean());
    }

    [Fact]
    public async Task Query_returns_a_hebrew_error_for_invalid_filters()
    {
        var response = await _client.PostAsJsonAsync("/api/query", new DealFilters { MinimumRooms = 5, MaximumRooms = 4 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_filters", body.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("message").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("requestId").GetString()));
    }

    [Fact]
    public async Task Query_returns_an_empty_result_when_no_deals_match()
    {
        var response = await _client.PostAsJsonAsync("/api/query", new DealFilters { City = "no-match" });

        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(0, body.RootElement.GetProperty("result").GetProperty("transactionCount").GetInt32());
    }

    [Fact]
    public async Task Deal_returns_conflict_evidence()
    {
        var response = await _client.GetAsync("/api/deals/D100017");

        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("conflicting", body.RootElement.GetProperty("conflictStatus").GetString());
        Assert.Equal(2, body.RootElement.GetProperty("reports").GetArrayLength());
    }

    [Fact]
    public async Task Missing_deal_returns_a_hebrew_not_found_error_without_stack_trace()
    {
        var response = await _client.GetAsync("/api/deals/not-found");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Exception", content, StringComparison.OrdinalIgnoreCase);
        using var body = JsonDocument.Parse(content);
        Assert.Equal("deal_not_found", body.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("message").GetString()));
    }
}
