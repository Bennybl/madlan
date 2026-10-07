using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MadlanExplorer;
using Xunit;

namespace MadlanExplorer.Tests;

public class MadlanApplicationServiceTests
{
    [Fact]
    public async Task Ask_returns_the_orchestrated_answer_with_the_dataset_hash()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.Agent, """{"outcome":"final","summary":"יש 243 עסקאות.","referencedDealIds":[]}""");
        provider.SetContent(LlmStage.Verification, """{"outcome":"approved"}""");
        var repository = new FakeDealRepository();
        var metadataProvider = new FakeDatasetMetadataProvider { Metadata = new DatasetMetadata { FileHash = "hash-xyz" } };
        var service = CreateService(provider, repository, metadataProvider);

        var response = await service.AskAsync("כמה עסקאות יש?", CancellationToken.None);

        Assert.Equal("query", response.Status);
        Assert.Equal("יש 243 עסקאות.", response.Summary);
        Assert.Equal("hash-xyz", response.DatasetHash);
        Assert.Null(response.Message);
    }

    [Fact]
    public async Task Ask_returns_clarification_from_the_orchestrator()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.Agent, """{"outcome":"clarification","message":"לאיזו שכונה כוונתך?"}""");
        var service = CreateService(provider, new FakeDealRepository());

        var response = await service.AskAsync("כמה עסקאות יש במרכז?", CancellationToken.None);

        Assert.Equal("clarification", response.Status);
        Assert.Equal("לאיזו שכונה כוונתך?", response.Message);
    }

    [Fact]
    public async Task Ask_returns_unsupported_from_the_orchestrator()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.Agent, """{"outcome":"unsupported","message":"אין לי תחזיות מחיר."}""");
        var service = CreateService(provider, new FakeDealRepository());

        var response = await service.AskAsync("מה יהיה המחיר בעוד שנה?", CancellationToken.None);

        Assert.Equal("unsupported", response.Status);
        Assert.Equal("אין לי תחזיות מחיר.", response.Message);
    }

    [Fact]
    public async Task Ask_returns_the_deal_directly_when_the_prompt_names_a_known_deal_id_without_calling_the_llm()
    {
        var provider = new FakeLlmProvider();
        var repository = new FakeDealRepository { Deal = new DealDetail { DealId = "D100027", ConflictStatus = "usable" } };
        var service = CreateService(provider, repository);

        var response = await service.AskAsync("תן לי את כל הפרטים של עסקה D100027", CancellationToken.None);

        Assert.Equal("deal", response.Status);
        Assert.Equal("D100027", response.DealId);
        Assert.Empty(provider.Requests);
    }

    [Fact]
    public async Task Ask_rejects_a_deal_id_shaped_prompt_that_does_not_match_a_real_deal()
    {
        var provider = new FakeLlmProvider();
        var repository = new FakeDealRepository { Deal = null };
        var service = CreateService(provider, repository);

        var response = await service.AskAsync("תן לי את הפרטים של עסקה D999999", CancellationToken.None);

        Assert.Equal("unsupported", response.Status);
        Assert.NotNull(response.Message);
        Assert.Empty(provider.Requests);
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
            ServerTimeoutSeconds = 600,
            Models = new LlmModelsOptions { Agent = "grok-agent-model", Verification = "grok-verification-model" }
        });

        var messages = Options.Create(new MessagesOptions { SummaryUnavailable = "summary-unavailable-message" });
        var queryService = new QueryService(repository);
        var orchestrationService = new QueryOrchestrationService(provider, options, queryService, repository, TestLocalityCatalog.CreateLoaded(), NullLogger<QueryOrchestrationService>.Instance);

        return new MadlanApplicationService(
            orchestrationService,
            queryService,
            repository,
            metadataProvider ?? new FakeDatasetMetadataProvider(),
            options,
            messages);
    }
}
