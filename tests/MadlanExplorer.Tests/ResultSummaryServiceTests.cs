using System.Text.Json;
using Microsoft.Extensions.Options;
using MadlanExplorer;
using Xunit;

namespace MadlanExplorer.Tests;

public class ResultSummaryServiceTests
{
    [Fact]
    public async Task Summarize_returns_a_grounded_summary_using_the_configured_model()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.ResultSummary, """{"summary":"נמצאו שלוש עסקאות.","referencedDealIds":["D100027"]}""");
        var service = CreateService(provider);
        var result = new DealQueryResult
        {
            TransactionCount = 1,
            MedianPriceNis = 3_826_000m,
            ContributorDealIds = ["D100027"],
            PriceContributorDealIds = ["D100027"],
            PricePerSqmContributorDealIds = ["D100027"]
        };

        var output = await service.SummarizeAsync("מה המחיר החציוני?", new DealFilters(), "hash-1", result, CancellationToken.None);

        Assert.Equal("נמצאו שלוש עסקאות.", output.Summary);
        Assert.Equal(["D100027"], output.ReferencedDealIds);
        Assert.Equal(LlmStage.ResultSummary, provider.Request?.Stage);
        Assert.Equal("grok-summary-model", provider.Request?.Model);
    }

    [Fact]
    public async Task Summarize_sends_the_dataset_hash_and_calculated_metrics_to_the_model()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.ResultSummary, """{"summary":"אין נתונים.","referencedDealIds":[]}""");
        var service = CreateService(provider);
        var result = new DealQueryResult { TransactionCount = 0 };

        await service.SummarizeAsync("שאלה", new DealFilters(), "hash-abc123", result, CancellationToken.None);

        var sentPrompt = provider.Request!.Prompt;
        Assert.Contains("hash-abc123", sentPrompt);
        Assert.Contains("\"transactionCount\":0", sentPrompt);
    }

    [Fact]
    public async Task Summarize_tells_the_model_never_to_make_claims_about_the_datasets_structure()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.ResultSummary, """{"summary":"אין נתונים.","referencedDealIds":[]}""");
        var service = CreateService(provider);
        var result = new DealQueryResult { TransactionCount = 0 };

        await service.SummarizeAsync("שאלה", new DealFilters(), "hash-abc123", result, CancellationToken.None);

        var sentPrompt = provider.Request!.Prompt;
        Assert.Contains("Never make claims about the dataset's structure", sentPrompt);
    }

    [Fact]
    public async Task Summarize_mentions_ranked_metrics_in_the_evidence_sent_to_the_model()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.ResultSummary, """{"summary":"הדירה השנייה הכי יקרה היא D100190.","referencedDealIds":["D100190"]}""");
        var service = CreateService(provider);
        var result = new DealQueryResult
        {
            TransactionCount = 5,
            RankedMetrics = [new RankedMetricResult { Metric = QueryMetric.MaxPrice, Rank = 2, Value = 5_000_000m, DealId = "D100190" }]
        };

        await service.SummarizeAsync("שאלה", new DealFilters(), "hash-abc123", result, CancellationToken.None);

        var sentPrompt = provider.Request!.Prompt;
        Assert.Contains("\"rank\":2", sentPrompt);
        Assert.Contains("D100190", sentPrompt);
    }

    [Fact]
    public async Task Summarize_allows_empty_references_for_an_empty_result()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.ResultSummary, """{"summary":"לא נמצאו עסקאות מתאימות.","referencedDealIds":[]}""");
        var service = CreateService(provider);
        var result = new DealQueryResult { TransactionCount = 0 };

        var output = await service.SummarizeAsync("שאלה", new DealFilters(), "hash-1", result, CancellationToken.None);

        Assert.Empty(output.ReferencedDealIds);
    }

    [Fact]
    public async Task Summarize_rejects_references_when_there_is_no_evidence()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.ResultSummary, """{"summary":"לא נמצאו עסקאות.","referencedDealIds":["D999999"]}""");
        var service = CreateService(provider);
        var result = new DealQueryResult { TransactionCount = 0 };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SummarizeAsync("שאלה", new DealFilters(), "hash-1", result, CancellationToken.None));
    }

    [Fact]
    public async Task Summarize_rejects_a_summary_with_no_references_when_evidence_exists()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.ResultSummary, """{"summary":"נמצאו עסקאות.","referencedDealIds":[]}""");
        var service = CreateService(provider);
        var result = new DealQueryResult
        {
            TransactionCount = 1,
            ContributorDealIds = ["D100027"],
            PriceContributorDealIds = ["D100027"],
            PricePerSqmContributorDealIds = ["D100027"]
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SummarizeAsync("שאלה", new DealFilters(), "hash-1", result, CancellationToken.None));
    }

    [Fact]
    public async Task Summarize_rejects_a_hallucinated_deal_id_reference()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.ResultSummary, """{"summary":"נמצאה עסקה.","referencedDealIds":["D999999"]}""");
        var service = CreateService(provider);
        var result = new DealQueryResult
        {
            TransactionCount = 1,
            ContributorDealIds = ["D100027"],
            PriceContributorDealIds = ["D100027"],
            PricePerSqmContributorDealIds = ["D100027"]
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SummarizeAsync("שאלה", new DealFilters(), "hash-1", result, CancellationToken.None));
    }

    [Fact]
    public async Task Summarize_rejects_an_empty_summary_text()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.ResultSummary, """{"summary":"","referencedDealIds":[]}""");
        var service = CreateService(provider);
        var result = new DealQueryResult { TransactionCount = 0 };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SummarizeAsync("שאלה", new DealFilters(), "hash-1", result, CancellationToken.None));
    }

    [Fact]
    public async Task Summarize_rejects_malformed_output()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.ResultSummary, "not-json");
        var service = CreateService(provider);
        var result = new DealQueryResult { TransactionCount = 0 };

        await Assert.ThrowsAsync<JsonException>(() => service.SummarizeAsync("שאלה", new DealFilters(), "hash-1", result, CancellationToken.None));
    }

    [Fact]
    public async Task Summarize_fails_clearly_when_the_summary_model_is_not_configured()
    {
        var provider = new FakeLlmProvider();
        var options = Options.Create(new LlmOptions());
        var service = new ResultSummaryService(provider, options);
        var result = new DealQueryResult { TransactionCount = 0 };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SummarizeAsync("שאלה", new DealFilters(), "hash-1", result, CancellationToken.None));
        Assert.Empty(provider.Requests);
    }

    private static ResultSummaryService CreateService(FakeLlmProvider provider)
    {
        var options = Options.Create(new LlmOptions { Models = new LlmModelsOptions { ResultSummary = "grok-summary-model" } });
        return new ResultSummaryService(provider, options);
    }
}
