using System.Text.Json;
using Microsoft.Extensions.Options;
using MadlanExplorer;
using Xunit;

namespace MadlanExplorer.Tests;

public class QueryVerificationServiceTests
{
    [Fact]
    public async Task Verify_approves_matching_filters_using_the_configured_model()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryVerification, """{"outcome":"approved"}""");
        var service = CreateService(provider);
        const string prompt = "מה מחיר דירת ארבעה חדרים בחולון?";

        var result = await service.VerifyAsync(prompt, new DealFilters { MinimumRooms = 4, MaximumRooms = 4 }, [], [], null, null, CancellationToken.None);

        Assert.Equal("approved", result.Outcome);
        Assert.Equal(LlmStage.QueryVerification, provider.Request?.Stage);
        Assert.Equal("grok-verification-model", provider.Request?.Model);
    }

    [Fact]
    public async Task Verify_rejects_filters_that_omit_a_stated_constraint()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryVerification, """{"outcome":"rejected","message":"The prompt asked for Holon but no city filter was proposed."}""");
        var service = CreateService(provider);

        var result = await service.VerifyAsync("דירות בחולון", new DealFilters(), [], [], null, null, CancellationToken.None);

        Assert.Equal("rejected", result.Outcome);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task Verify_asks_for_clarification_on_an_ambiguous_neighborhood()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryVerification, """{"outcome":"clarification","message":"Which neighborhood did you mean?"}""");
        var service = CreateService(provider);

        var result = await service.VerifyAsync("דירות ברובע המרכזי", new DealFilters(), [], [], null, null, CancellationToken.None);

        Assert.Equal("clarification", result.Outcome);
        Assert.Equal("Which neighborhood did you mean?", result.Message);
    }

    [Fact]
    public async Task Verify_approves_a_proposal_with_multiple_requested_metrics()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryVerification, """{"outcome":"approved"}""");
        var service = CreateService(provider);

        var result = await service.VerifyAsync(
            "מה המחיר הממוצע והחציוני בחולון?",
            new DealFilters { City = "חולון" },
            [QueryMetric.AveragePrice, QueryMetric.MedianPrice],
            [],
            null,
            null,
            CancellationToken.None);

        Assert.Equal("approved", result.Outcome);
        Assert.Contains("AveragePrice", provider.Request?.Prompt);
        Assert.Contains("MedianPrice", provider.Request?.Prompt);
    }

    [Fact]
    public async Task Verify_includes_a_proposed_ranked_metric_in_the_prompt()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryVerification, """{"outcome":"approved"}""");
        var service = CreateService(provider);

        var result = await service.VerifyAsync(
            "מה הדירה השנייה הכי יקרה בחולון?",
            new DealFilters { City = "חולון" },
            [],
            [new RankedMetricRequest { Metric = QueryMetric.MaxPrice, Rank = 2 }],
            null,
            null,
            CancellationToken.None);

        Assert.Equal("approved", result.Outcome);
        Assert.Contains("MaxPrice rank 2", provider.Request?.Prompt);
    }

    [Fact]
    public async Task Verify_includes_a_proposed_group_by_field_in_the_prompt()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryVerification, """{"outcome":"approved"}""");
        var service = CreateService(provider);

        var result = await service.VerifyAsync(
            "מה הדירה הזולה ביותר בכל עיר?",
            new DealFilters(),
            [],
            [new RankedMetricRequest { Metric = QueryMetric.MinPrice, Rank = 1 }],
            GroupByField.City,
            null,
            CancellationToken.None);

        Assert.Equal("approved", result.Outcome);
        Assert.Contains("City", provider.Request?.Prompt);
    }

    [Fact]
    public async Task Verify_includes_a_proposed_outlier_field_in_the_prompt()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryVerification, """{"outcome":"approved"}""");
        var service = CreateService(provider);

        var result = await service.VerifyAsync(
            "מה הדירות החריגות במחיר בכל עיר?",
            new DealFilters(),
            [],
            [],
            GroupByField.City,
            [OutlierField.Price],
            CancellationToken.None);

        Assert.Equal("approved", result.Outcome);
        Assert.Contains("Price", provider.Request?.Prompt);
    }

    [Fact]
    public async Task Verify_rejects_malformed_output()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryVerification, "not-json");
        var service = CreateService(provider);

        await Assert.ThrowsAsync<JsonException>(() => service.VerifyAsync("test", new DealFilters(), [], [], null, null, CancellationToken.None));
    }

    [Fact]
    public async Task Verify_rejects_an_unsupported_outcome_value()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryVerification, """{"outcome":"maybe"}""");
        var service = CreateService(provider);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.VerifyAsync("test", new DealFilters(), [], [], null, null, CancellationToken.None));
    }

    [Fact]
    public async Task Verify_fails_clearly_when_the_verification_model_is_not_configured()
    {
        var provider = new FakeLlmProvider();
        var options = Options.Create(new LlmOptions());
        var service = new QueryVerificationService(provider, options, new FakeDealRepository());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.VerifyAsync("test", new DealFilters(), [], [], null, null, CancellationToken.None));
        Assert.Empty(provider.Requests);
    }

    [Fact]
    public async Task Verify_includes_the_datasets_exact_property_types_in_the_prompt()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryVerification, """{"outcome":"approved"}""");
        var repository = new FakeDealRepository { Facts = new DatasetFacts { PropertyTypes = ["דירה", "פנטהאוז"] } };
        var service = CreateService(provider, repository);

        await service.VerifyAsync("test", new DealFilters(), [], [], null, null, CancellationToken.None);

        Assert.Contains("דירה", provider.Request?.Prompt);
        Assert.Contains("פנטהאוז", provider.Request?.Prompt);
    }

    [Fact]
    public async Task Verify_includes_the_datasets_exact_condition_and_source_values_in_the_prompt()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryVerification, """{"outcome":"approved"}""");
        var repository = new FakeDealRepository { Facts = new DatasetFacts { Conditions = ["חדש מקבלן"], Sources = ["בעל נכס"] } };
        var service = CreateService(provider, repository);

        await service.VerifyAsync("test", new DealFilters(), [], [], null, null, CancellationToken.None);

        Assert.Contains("חדש מקבלן", provider.Request?.Prompt);
        Assert.Contains("בעל נכס", provider.Request?.Prompt);
    }

    private static QueryVerificationService CreateService(FakeLlmProvider provider, FakeDealRepository? repository = null)
    {
        var options = Options.Create(new LlmOptions { Models = new LlmModelsOptions { QueryVerification = "grok-verification-model" } });
        return new QueryVerificationService(provider, options, repository ?? new FakeDealRepository());
    }
}
