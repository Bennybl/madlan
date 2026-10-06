using Microsoft.Extensions.Options;
using MadlanExplorer;
using Xunit;

namespace MadlanExplorer.Tests;

public class QueryOrchestrationServiceTests
{
    [Fact]
    public async Task RunAsync_answers_immediately_when_the_agent_needs_no_data()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.Agent, """{"outcome":"final","summary":"זו שאלה כללית.","referencedDealIds":[]}""");
        provider.SetContent(LlmStage.Verification, """{"outcome":"approved"}""");
        var service = CreateService(provider, new FakeDealRepository());

        var result = await service.RunAsync("test", CancellationToken.None);

        Assert.Equal("query", result.Status);
        Assert.Equal("זו שאלה כללית.", result.Summary);
        Assert.Empty(result.Steps);
    }

    [Fact]
    public async Task RunAsync_executes_a_requested_query_and_feeds_the_real_result_back()
    {
        var provider = new FakeLlmProvider();
        provider.SetContentSequence(
            LlmStage.Agent,
            """{"outcome":"query","query":{"filters":{"city":"חולון"},"aggregate":"count"}}""",
            """{"outcome":"final","summary":"יש 7 עסקאות בחולון.","referencedDealIds":[]}""");
        provider.SetContent(LlmStage.Verification, """{"outcome":"approved"}""");
        var repository = new FakeDealRepository { DataResult = new DataQueryResult { TransactionCount = 7 } };
        var service = CreateService(provider, repository);

        var result = await service.RunAsync("כמה עסקאות בחולון?", CancellationToken.None);

        Assert.Equal("query", result.Status);
        Assert.Equal("יש 7 עסקאות בחולון.", result.Summary);
        var step = Assert.Single(result.Steps);
        Assert.Equal("חולון", step.Query.Filters.City);
        Assert.Equal(DataAggregate.Count, step.Query.Aggregate);
        Assert.Equal(7, step.Result.TransactionCount);
        Assert.Single(repository.ReceivedDataQueries);
    }

    [Fact]
    public async Task RunAsync_supports_a_multi_step_plan_before_concluding()
    {
        var provider = new FakeLlmProvider();
        provider.SetContentSequence(
            LlmStage.Agent,
            """{"outcome":"query","query":{"groupBy":"City","aggregate":"average","field":"price","limit":5}}""",
            """{"outcome":"query","query":{"groupBy":"City","aggregate":"min","field":"price"}}""",
            """{"outcome":"final","summary":"הדירה הזולה מבין חמש הערים היקרות היא D1.","referencedDealIds":["D1"]}""");
        provider.SetContent(LlmStage.Verification, """{"outcome":"approved"}""");
        var repository = new FakeDealRepository
        {
            DataResultFactory = query => query.Aggregate == DataAggregate.Average
                ? new DataQueryResult { TransactionCount = 100, Rows = [new DataRow { GroupValue = "תל אביב", Value = 9_000_000m }] }
                : new DataQueryResult { TransactionCount = 100, Rows = [new DataRow { GroupValue = "תל אביב", Value = 2_000_000m, DealId = "D1" }] }
        };
        var service = CreateService(provider, repository);

        var result = await service.RunAsync("מה הדירה הכי זולה מבין חמשת הערים הכי יקרות?", CancellationToken.None);

        Assert.Equal("query", result.Status);
        Assert.Equal("הדירה הזולה מבין חמש הערים היקרות היא D1.", result.Summary);
        Assert.Equal(2, result.Steps.Count);
        Assert.Equal(2, repository.ReceivedDataQueries.Count);
    }

    [Fact]
    public async Task RunAsync_returns_clarification_from_the_agent()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.Agent, """{"outcome":"clarification","message":"לאיזו שכונה כוונתך?"}""");
        var service = CreateService(provider, new FakeDealRepository());

        var result = await service.RunAsync("test", CancellationToken.None);

        Assert.Equal("clarification", result.Status);
        Assert.Equal("לאיזו שכונה כוונתך?", result.Message);
    }

    [Fact]
    public async Task RunAsync_returns_unsupported_from_the_agent()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.Agent, """{"outcome":"unsupported","message":"אין לי תחזיות."}""");
        var service = CreateService(provider, new FakeDealRepository());

        var result = await service.RunAsync("test", CancellationToken.None);

        Assert.Equal("unsupported", result.Status);
        Assert.Equal("אין לי תחזיות.", result.Message);
    }

    [Fact]
    public async Task RunAsync_falls_back_to_no_summary_when_the_final_answer_references_an_unknown_deal_id()
    {
        var provider = new FakeLlmProvider();
        provider.SetContentSequence(
            LlmStage.Agent,
            """{"outcome":"query","query":{"aggregate":"count"}}""",
            """{"outcome":"final","summary":"העסקה D999999 היא הזולה ביותר.","referencedDealIds":["D999999"]}""");
        var repository = new FakeDealRepository { DataResult = new DataQueryResult { TransactionCount = 5, Rows = [new DataRow { Value = 5 }] } };
        var service = CreateService(provider, repository);

        var result = await service.RunAsync("test", CancellationToken.None);

        Assert.Equal("query", result.Status);
        Assert.Null(result.Summary);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
        Assert.Single(result.Steps);
    }

    [Fact]
    public async Task RunAsync_falls_back_to_no_summary_when_final_verification_rejects()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.Agent, """{"outcome":"final","summary":"סיכום כלשהו.","referencedDealIds":[]}""");
        provider.SetContent(LlmStage.Verification, """{"outcome":"rejected","message":"Answers a different question."}""");
        var service = CreateService(provider, new FakeDealRepository());

        var result = await service.RunAsync("test", CancellationToken.None);

        Assert.Equal("query", result.Status);
        Assert.Null(result.Summary);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Fact]
    public async Task RunAsync_stops_after_the_iteration_budget_and_reports_unsupported()
    {
        var provider = new FakeLlmProvider { Content = """{"outcome":"query","query":{"aggregate":"count"}}""" };
        var repository = new FakeDealRepository { DataResult = new DataQueryResult { TransactionCount = 1 } };
        var service = CreateService(provider, repository);

        var result = await service.RunAsync("test", CancellationToken.None);

        Assert.Equal("unsupported", result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
        Assert.Equal(10, result.Steps.Count);
        Assert.Equal(10, provider.Requests.Count);
    }

    [Fact]
    public async Task RunAsync_resolves_a_clear_city_typo_in_a_requested_query()
    {
        var provider = new FakeLlmProvider();
        provider.SetContentSequence(
            LlmStage.Agent,
            """{"outcome":"query","query":{"filters":{"city":"ABU GHOS"},"aggregate":"count"}}""",
            """{"outcome":"final","summary":"בוצע.","referencedDealIds":[]}""");
        var repository = new FakeDealRepository { DataResult = new DataQueryResult { TransactionCount = 2 } };
        var service = CreateService(provider, repository);

        await service.RunAsync("test", CancellationToken.None);

        var query = Assert.Single(repository.ReceivedDataQueries);
        Assert.Equal("אבו גוש", query.Filters.City);
    }

    [Fact]
    public async Task RunAsync_rejects_an_unsupported_group_by_field_in_a_requested_query()
    {
        var provider = new FakeLlmProvider { Content = """{"outcome":"query","query":{"groupBy":"NotAField","aggregate":"count"}}""" };
        var service = CreateService(provider, new FakeDealRepository());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunAsync("test", CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_fails_clearly_when_the_agent_model_is_not_configured()
    {
        var provider = new FakeLlmProvider();
        var repository = new FakeDealRepository();
        var options = Options.Create(new LlmOptions());
        var service = new QueryOrchestrationService(provider, options, new QueryService(repository), repository, TestLocalityCatalog.CreateLoaded());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunAsync("test", CancellationToken.None));
        Assert.Empty(provider.Requests);
    }

    [Fact]
    public async Task RunAsync_includes_the_datasets_exact_property_condition_and_source_values_in_the_prompt()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.Agent, """{"outcome":"final","summary":"x","referencedDealIds":[]}""");
        var repository = new FakeDealRepository
        {
            Facts = new DatasetFacts { PropertyTypes = ["דירה"], Conditions = ["חדש מקבלן"], Sources = ["בעל נכס"] }
        };
        var service = CreateService(provider, repository);

        await service.RunAsync("test", CancellationToken.None);

        var agentPrompt = provider.Requests.First(r => r.Stage == LlmStage.Agent).Prompt;
        Assert.Contains("דירה", agentPrompt);
        Assert.Contains("חדש מקבלן", agentPrompt);
        Assert.Contains("בעל נכס", agentPrompt);
    }

    private static QueryOrchestrationService CreateService(FakeLlmProvider provider, FakeDealRepository repository)
    {
        var options = Options.Create(new LlmOptions
        {
            Models = new LlmModelsOptions { Agent = "grok-agent-model", Verification = "grok-verification-model" }
        });

        return new QueryOrchestrationService(provider, options, new QueryService(repository), repository, TestLocalityCatalog.CreateLoaded());
    }
}
