using System.Text.Json;
using Microsoft.Extensions.Options;
using MadlanExplorer;
using Xunit;

namespace MadlanExplorer.Tests;

public class MadlanApplicationServiceTests
{
    [Fact]
    public async Task Ask_returns_a_verified_summary_when_all_four_stages_approve()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryGeneration, """{"outcome":"query","filters":{"minimumRooms":4,"maximumRooms":4}}""");
        provider.SetContent(LlmStage.QueryVerification, """{"outcome":"approved"}""");
        provider.SetContent(LlmStage.ResultSummary, """{"summary":"נמצאה עסקה אחת בחולון.","referencedDealIds":["D100027"]}""");
        provider.SetContent(LlmStage.ResultVerification, """{"outcome":"approved"}""");
        var repository = new FakeDealRepository
        {
            Result = new DealQueryResult
            {
                TransactionCount = 1,
                MedianPriceNis = 3_826_000m,
                ContributorDealIds = ["D100027"],
                PriceContributorDealIds = ["D100027"],
                PricePerSqmContributorDealIds = ["D100027"]
            }
        };
        var metadataProvider = new FakeDatasetMetadataProvider { Metadata = new DatasetMetadata { FileHash = "hash-xyz" } };
        var service = CreateService(provider, repository, metadataProvider);
        const string prompt = "מה מחיר דירת ארבעה חדרים בחולון?";

        var response = await service.AskAsync(prompt, CancellationToken.None);

        Assert.Equal("query", response.Status);
        Assert.Equal(4, response.Filters?.MinimumRooms);
        Assert.Equal(1, response.Result?.TransactionCount);
        Assert.Equal("נמצאה עסקה אחת בחולון.", response.Summary);
        Assert.Equal("hash-xyz", response.DatasetHash);
        Assert.Null(response.Message);
        Assert.Equal(
            [LlmStage.QueryGeneration, LlmStage.QueryVerification, LlmStage.ResultSummary, LlmStage.ResultVerification],
            provider.Requests.Select(r => r.Stage));
    }

    [Fact]
    public async Task Ask_shows_deterministic_results_without_a_summary_when_the_summary_model_fails()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryGeneration, """{"outcome":"query","filters":{"minimumRooms":4,"maximumRooms":4}}""");
        provider.SetContent(LlmStage.QueryVerification, """{"outcome":"approved"}""");
        provider.SetContent(LlmStage.ResultSummary, "not-json");
        var repository = new FakeDealRepository { Result = new DealQueryResult { TransactionCount = 1 } };
        var service = CreateService(provider, repository);

        var response = await service.AskAsync("test", CancellationToken.None);

        Assert.Equal("query", response.Status);
        Assert.NotNull(response.Result);
        Assert.Null(response.Summary);
        Assert.False(string.IsNullOrWhiteSpace(response.Message));
    }

    [Fact]
    public async Task Ask_shows_deterministic_results_without_a_summary_when_result_verification_rejects()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryGeneration, """{"outcome":"query","filters":{"minimumRooms":4,"maximumRooms":4}}""");
        provider.SetContent(LlmStage.QueryVerification, """{"outcome":"approved"}""");
        provider.SetContent(LlmStage.ResultSummary, """{"summary":"נמצאה עסקה.","referencedDealIds":["D100027"]}""");
        provider.SetContent(LlmStage.ResultVerification, """{"outcome":"rejected","message":"The summary answers a different question."}""");
        var repository = new FakeDealRepository
        {
            Result = new DealQueryResult
            {
                TransactionCount = 1,
                ContributorDealIds = ["D100027"],
                PriceContributorDealIds = ["D100027"],
                PricePerSqmContributorDealIds = ["D100027"]
            }
        };
        var service = CreateService(provider, repository);

        var response = await service.AskAsync("test", CancellationToken.None);

        Assert.Equal("query", response.Status);
        Assert.NotNull(response.Result);
        Assert.Null(response.Summary);
        Assert.False(string.IsNullOrWhiteSpace(response.Message));
    }

    [Fact]
    public async Task Ask_returns_clarification_without_filters_or_results_when_query_verification_rejects()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryGeneration, """{"outcome":"query","filters":{"minimumRooms":4,"maximumRooms":4}}""");
        provider.SetContent(LlmStage.QueryVerification, """{"outcome":"rejected","message":"Room bounds do not match the prompt."}""");
        var repository = new FakeDealRepository();
        var service = CreateService(provider, repository);

        var response = await service.AskAsync("test", CancellationToken.None);

        Assert.Equal("clarification", response.Status);
        Assert.Null(response.Filters);
        Assert.Null(response.Result);
        Assert.Null(response.Summary);
        Assert.Equal("Room bounds do not match the prompt.", response.Message);
        Assert.Null(repository.ReceivedQuery);
    }

    [Fact]
    public async Task Ask_returns_clarification_without_filters_when_query_verification_is_ambiguous()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryGeneration, """{"outcome":"query","filters":{"minimumRooms":4,"maximumRooms":4}}""");
        provider.SetContent(LlmStage.QueryVerification, """{"outcome":"clarification","message":"Which neighborhood did you mean?"}""");
        var service = CreateService(provider, new FakeDealRepository());

        var response = await service.AskAsync("test", CancellationToken.None);

        Assert.Equal("clarification", response.Status);
        Assert.Null(response.Filters);
        Assert.Equal("Which neighborhood did you mean?", response.Message);
    }

    [Fact]
    public async Task Ask_skips_later_stages_when_generation_already_asked_for_clarification()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryGeneration, """{"outcome":"clarification","message":"Which city did you mean?"}""");
        var service = CreateService(provider, new FakeDealRepository());

        var response = await service.AskAsync("test", CancellationToken.None);

        Assert.Equal("clarification", response.Status);
        Assert.Single(provider.Requests);
        Assert.Equal(LlmStage.QueryGeneration, provider.Requests[0].Stage);
    }

    [Fact]
    public async Task Ask_skips_later_stages_when_the_request_is_unsupported()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryGeneration, """{"outcome":"unsupported","message":"We cannot provide valuations."}""");
        var service = CreateService(provider, new FakeDealRepository());

        var response = await service.AskAsync("test", CancellationToken.None);

        Assert.Equal("unsupported", response.Status);
        Assert.Single(provider.Requests);
    }

    [Fact]
    public async Task Ask_propagates_an_exception_when_the_query_verifier_is_unavailable()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryGeneration, """{"outcome":"query","filters":{"minimumRooms":4,"maximumRooms":4}}""");
        provider.SetContent(LlmStage.QueryVerification, "not-json");
        var service = CreateService(provider, new FakeDealRepository());

        await Assert.ThrowsAsync<JsonException>(() => service.AskAsync("test", CancellationToken.None));
    }

    [Fact]
    public async Task Ask_treats_an_internal_summary_stage_failure_as_summary_unavailable_not_as_caller_cancellation()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryGeneration, """{"outcome":"query","filters":{"minimumRooms":4,"maximumRooms":4}}""");
        provider.SetContent(LlmStage.QueryVerification, """{"outcome":"approved"}""");
        provider.SetException(LlmStage.ResultSummary, new OperationCanceledException());
        var repository = new FakeDealRepository { Result = new DealQueryResult { TransactionCount = 1 } };
        var service = CreateService(provider, repository);

        var response = await service.AskAsync("test", CancellationToken.None);

        Assert.Equal("query", response.Status);
        Assert.Null(response.Summary);
        Assert.False(string.IsNullOrWhiteSpace(response.Message));
    }

    [Fact]
    public async Task Ask_propagates_cancellation_when_the_caller_actually_cancelled_during_the_summary_stage()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryGeneration, """{"outcome":"query","filters":{"minimumRooms":4,"maximumRooms":4}}""");
        provider.SetContent(LlmStage.QueryVerification, """{"outcome":"approved"}""");
        provider.SetException(LlmStage.ResultSummary, new OperationCanceledException(cts.Token));
        var repository = new FakeDealRepository { Result = new DealQueryResult { TransactionCount = 1 } };
        var service = CreateService(provider, repository);

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.AskAsync("test", cts.Token));
    }

    [Fact]
    public void Query_returns_the_calculated_result_and_dataset_hash_without_calling_the_llm()
    {
        var provider = new FakeLlmProvider();
        var repository = new FakeDealRepository { Result = new DealQueryResult { TransactionCount = 3 } };
        var metadataProvider = new FakeDatasetMetadataProvider { Metadata = new DatasetMetadata { FileHash = "hash-manual" } };
        var service = CreateService(provider, repository, metadataProvider);
        var filters = new DealFilters { City = "חולון" };

        var response = service.Query(filters);

        Assert.Same(filters, response.Filters);
        Assert.Equal(3, response.Result.TransactionCount);
        Assert.Equal("hash-manual", response.DatasetHash);
        Assert.Empty(provider.Requests);
    }

    [Fact]
    public void Query_propagates_invalid_filter_bounds()
    {
        var service = CreateService(new FakeLlmProvider(), new FakeDealRepository());

        Assert.Throws<ArgumentException>(() => service.Query(new DealFilters { MinimumRooms = 5, MaximumRooms = 4 }));
    }

    [Fact]
    public void GetDatasetSummary_combines_repository_facts_and_dataset_metadata()
    {
        var repository = new FakeDealRepository
        {
            Facts = new DatasetFacts
            {
                DealCount = 520,
                UsableDealCount = 516,
                ConflictingDealCount = 4,
                Cities = ["חולון"],
                Neighborhoods = ["מרכז"],
                PropertyTypes = ["דירה"]
            }
        };
        var metadataProvider = new FakeDatasetMetadataProvider { Metadata = new DatasetMetadata { FileHash = "hash-1", ReportCount = 530 } };
        var service = CreateService(new FakeLlmProvider(), repository, metadataProvider);

        var summary = service.GetDatasetSummary();

        Assert.Equal("hash-1", summary.DatasetHash);
        Assert.Equal(530, summary.ReportCount);
        Assert.Equal(520, summary.DealCount);
        Assert.Equal(516, summary.UsableDealCount);
        Assert.Equal(4, summary.ConflictingDealCount);
        Assert.Equal(["חולון"], summary.Cities);
    }

    [Fact]
    public void GetDeal_returns_the_detail_the_repository_has_for_a_known_deal()
    {
        var detail = new DealDetail { DealId = "D100027", ConflictStatus = "usable" };
        var repository = new FakeDealRepository { Deal = detail };
        var service = CreateService(new FakeLlmProvider(), repository);

        var result = service.GetDeal("D100027");

        Assert.Same(detail, result);
    }

    [Fact]
    public void GetDeal_returns_null_for_an_unknown_deal()
    {
        var service = CreateService(new FakeLlmProvider(), new FakeDealRepository());

        Assert.Null(service.GetDeal("unknown"));
    }

    private static MadlanApplicationService CreateService(
        FakeLlmProvider provider,
        FakeDealRepository repository,
        FakeDatasetMetadataProvider? metadataProvider = null)
    {
        var options = Options.Create(new LlmOptions
        {
            Models = new LlmModelsOptions
            {
                QueryGeneration = "grok-generation-model",
                QueryVerification = "grok-verification-model",
                ResultSummary = "grok-summary-model",
                ResultVerification = "grok-result-verification-model"
            }
        });

        var messages = Options.Create(new MessagesOptions { SummaryUnavailable = "summary-unavailable-message" });

        var queryService = new QueryService(repository);
        var generationService = new QueryGenerationService(provider, options, queryService, TestLocalityCatalog.CreateLoaded(), repository);
        var verificationService = new QueryVerificationService(provider, options, repository);
        var summaryService = new ResultSummaryService(provider, options);
        var resultVerificationService = new ResultVerificationService(provider, options);

        return new MadlanApplicationService(
            generationService,
            verificationService,
            queryService,
            summaryService,
            resultVerificationService,
            repository,
            metadataProvider ?? new FakeDatasetMetadataProvider(),
            options,
            messages);
    }
}
