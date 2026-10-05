using System.Text.Json;
using Microsoft.Extensions.Options;
using MadlanExplorer;
using Xunit;

namespace MadlanExplorer.Tests;

public class ResultVerificationServiceTests
{
    [Fact]
    public async Task Verify_approves_a_summary_that_matches_the_evidence_using_the_configured_model()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.ResultVerification, """{"outcome":"approved"}""");
        var service = CreateService(provider);
        var result = new DealQueryResult
        {
            TransactionCount = 1,
            ContributorDealIds = ["D100027"],
            PriceContributorDealIds = ["D100027"],
            PricePerSqmContributorDealIds = ["D100027"]
        };
        var candidate = new ResultSummaryOutput { Summary = "נמצאה עסקה אחת.", ReferencedDealIds = ["D100027"] };

        var output = await service.VerifyAsync("שאלה", new DealFilters(), "hash-1", result, candidate, CancellationToken.None);

        Assert.Equal("approved", output.Outcome);
        Assert.Equal(LlmStage.ResultVerification, provider.Request?.Stage);
        Assert.Equal("grok-result-verification-model", provider.Request?.Model);
    }

    [Fact]
    public async Task Verify_rejects_a_summary_that_answers_a_different_question()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.ResultVerification, """{"outcome":"rejected","message":"The summary answers a different city than the one asked."}""");
        var service = CreateService(provider);
        var result = new DealQueryResult
        {
            TransactionCount = 1,
            ContributorDealIds = ["D100027"],
            PriceContributorDealIds = ["D100027"],
            PricePerSqmContributorDealIds = ["D100027"]
        };
        var candidate = new ResultSummaryOutput { Summary = "נמצאה עסקה.", ReferencedDealIds = ["D100027"] };

        var output = await service.VerifyAsync("שאלה", new DealFilters(), "hash-1", result, candidate, CancellationToken.None);

        Assert.Equal("rejected", output.Outcome);
        Assert.NotNull(output.Message);
    }

    [Fact]
    public async Task Verify_rejects_without_calling_the_model_when_the_candidate_cites_a_deal_id_outside_the_evidence()
    {
        var provider = new FakeLlmProvider();
        var service = CreateService(provider);
        var result = new DealQueryResult
        {
            TransactionCount = 1,
            ContributorDealIds = ["D100027"],
            PriceContributorDealIds = ["D100027"],
            PricePerSqmContributorDealIds = ["D100027"]
        };
        var candidate = new ResultSummaryOutput { Summary = "נמצאה עסקה.", ReferencedDealIds = ["D999999"] };

        var output = await service.VerifyAsync("שאלה", new DealFilters(), "hash-1", result, candidate, CancellationToken.None);

        Assert.Equal("rejected", output.Outcome);
        Assert.Empty(provider.Requests);
    }

    [Fact]
    public async Task Verify_rejects_malformed_output()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.ResultVerification, "not-json");
        var service = CreateService(provider);
        var result = new DealQueryResult { TransactionCount = 0 };
        var candidate = new ResultSummaryOutput { Summary = "לא נמצאו עסקאות.", ReferencedDealIds = [] };

        await Assert.ThrowsAsync<JsonException>(() => service.VerifyAsync("שאלה", new DealFilters(), "hash-1", result, candidate, CancellationToken.None));
    }

    [Fact]
    public async Task Verify_rejects_an_unsupported_outcome_value()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.ResultVerification, """{"outcome":"clarification"}""");
        var service = CreateService(provider);
        var result = new DealQueryResult { TransactionCount = 0 };
        var candidate = new ResultSummaryOutput { Summary = "לא נמצאו עסקאות.", ReferencedDealIds = [] };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.VerifyAsync("שאלה", new DealFilters(), "hash-1", result, candidate, CancellationToken.None));
    }

    [Fact]
    public async Task Verify_fails_clearly_when_the_result_verification_model_is_not_configured()
    {
        var provider = new FakeLlmProvider();
        var options = Options.Create(new LlmOptions());
        var service = new ResultVerificationService(provider, options);
        var result = new DealQueryResult { TransactionCount = 0 };
        var candidate = new ResultSummaryOutput { Summary = "לא נמצאו עסקאות.", ReferencedDealIds = [] };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.VerifyAsync("שאלה", new DealFilters(), "hash-1", result, candidate, CancellationToken.None));
        Assert.Empty(provider.Requests);
    }

    private static ResultVerificationService CreateService(FakeLlmProvider provider)
    {
        var options = Options.Create(new LlmOptions { Models = new LlmModelsOptions { ResultVerification = "grok-result-verification-model" } });
        return new ResultVerificationService(provider, options);
    }
}
