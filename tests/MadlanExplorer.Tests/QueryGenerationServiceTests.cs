using System.Text.Json;
using Microsoft.Extensions.Options;
using MadlanExplorer;
using Xunit;

namespace MadlanExplorer.Tests;

public class QueryGenerationServiceTests
{
    [Fact]
    public async Task Generate_uses_the_configured_model_and_preserves_the_prompt()
    {
        var provider = new FakeLlmProvider();
        var service = CreateService(provider);
        const string prompt = "מה מחיר דירת ארבעה חדרים בחולון?";

        var response = await service.GenerateAsync(prompt, CancellationToken.None);

        Assert.Equal(prompt, response.Prompt);
        Assert.Equal("query", response.Status);
        Assert.Equal(4, response.Filters?.MinimumRooms);
        Assert.Equal(LlmStage.QueryGeneration, provider.Request?.Stage);
        Assert.Equal("grok-test-model", provider.Request?.Model);
    }

    [Fact]
    public async Task Generate_rejects_malformed_output()
    {
        var provider = new FakeLlmProvider { Content = "not-json" };
        var service = CreateService(provider);

        await Assert.ThrowsAsync<JsonException>(() => service.GenerateAsync("test", CancellationToken.None));
    }

    [Fact]
    public async Task Generate_corrects_a_clear_city_typo_against_the_locality_catalog()
    {
        var provider = new FakeLlmProvider { Content = """{"outcome":"query","filters":{"city":"ABU GHOS"}}""" };
        var service = CreateService(provider);

        var response = await service.GenerateAsync("How many deals were there in ABU GHOS?", CancellationToken.None);

        Assert.Equal("query", response.Status);
        Assert.Equal("אבו גוש", response.Filters?.City);
    }

    [Fact]
    public async Task Generate_returns_every_metric_the_question_asked_for()
    {
        var provider = new FakeLlmProvider { Content = """{"outcome":"query","filters":{"city":"חולון"},"metrics":["AveragePrice","MedianPrice"]}""" };
        var service = CreateService(provider);

        var response = await service.GenerateAsync("מה המחיר הממוצע והחציוני בחולון?", CancellationToken.None);

        Assert.Equal("query", response.Status);
        Assert.Equal([QueryMetric.AveragePrice, QueryMetric.MedianPrice], response.Metrics);
    }

    [Fact]
    public async Task Generate_defaults_to_no_extra_metrics_when_none_are_specified()
    {
        var provider = new FakeLlmProvider();
        var service = CreateService(provider);

        var response = await service.GenerateAsync("test", CancellationToken.None);

        Assert.Empty(response.Metrics ?? []);
    }

    [Fact]
    public async Task Generate_returns_a_ranked_metric_for_an_nth_highest_question()
    {
        var provider = new FakeLlmProvider { Content = """{"outcome":"query","filters":{},"rankedMetrics":[{"metric":"MaxPrice","rank":2}]}""" };
        var service = CreateService(provider);

        var response = await service.GenerateAsync("מה הדירה השנייה הכי יקרה?", CancellationToken.None);

        Assert.Equal("query", response.Status);
        var ranked = Assert.Single(response.RankedMetrics ?? []);
        Assert.Equal(QueryMetric.MaxPrice, ranked.Metric);
        Assert.Equal(2, ranked.Rank);
    }

    [Fact]
    public async Task Generate_rejects_a_ranked_metric_that_is_not_a_min_or_max_metric()
    {
        var provider = new FakeLlmProvider { Content = """{"outcome":"query","filters":{},"rankedMetrics":[{"metric":"AveragePrice","rank":2}]}""" };
        var service = CreateService(provider);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateAsync("test", CancellationToken.None));
    }

    [Fact]
    public async Task Generate_returns_a_group_by_field_for_a_per_category_breakdown_question()
    {
        var provider = new FakeLlmProvider { Content = """{"outcome":"query","filters":{},"rankedMetrics":[{"metric":"MinPrice","rank":1}],"groupBy":"City"}""" };
        var service = CreateService(provider);

        var response = await service.GenerateAsync("מה הדירה הזולה ביותר בכל עיר?", CancellationToken.None);

        Assert.Equal("query", response.Status);
        Assert.Equal(GroupByField.City, response.GroupBy);
    }

    [Fact]
    public async Task Generate_rejects_an_unsupported_group_by_field()
    {
        var provider = new FakeLlmProvider { Content = """{"outcome":"query","filters":{},"groupBy":"NotAField"}""" };
        var service = CreateService(provider);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateAsync("test", CancellationToken.None));
    }

    [Fact]
    public async Task Generate_returns_an_outlier_field_for_an_anomaly_question()
    {
        var provider = new FakeLlmProvider { Content = """{"outcome":"query","filters":{},"groupBy":"City","outlierFields":["Price"]}""" };
        var service = CreateService(provider);

        var response = await service.GenerateAsync("אילו דירות חריגות במחיר יש בכל עיר?", CancellationToken.None);

        Assert.Equal("query", response.Status);
        Assert.Equal([OutlierField.Price], response.OutlierFields);
        Assert.Equal(GroupByField.City, response.GroupBy);
    }

    [Fact]
    public async Task Generate_returns_every_outlier_field_for_an_every_field_question()
    {
        var provider = new FakeLlmProvider
        {
            Content = """{"outcome":"query","filters":{},"outlierFields":["Price","PricePerSqm","SizeSqm","Rooms","Floor","YearBuilt"]}"""
        };
        var service = CreateService(provider);

        var response = await service.GenerateAsync("בכל שדה", CancellationToken.None);

        Assert.Equal("query", response.Status);
        Assert.Equal(6, response.OutlierFields?.Count);
    }

    [Fact]
    public async Task Generate_rejects_an_unsupported_outlier_field()
    {
        var provider = new FakeLlmProvider { Content = """{"outcome":"query","filters":{},"outlierFields":["NotAField"]}""" };
        var service = CreateService(provider);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateAsync("test", CancellationToken.None));
    }

    [Fact]
    public async Task Generate_rejects_an_unsupported_metric_name()
    {
        var provider = new FakeLlmProvider { Content = """{"outcome":"query","filters":{},"metrics":["NotARealMetric"]}""" };
        var service = CreateService(provider);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateAsync("test", CancellationToken.None));
    }

    [Fact]
    public async Task Generate_leaves_an_unresolved_city_unchanged()
    {
        var provider = new FakeLlmProvider { Content = """{"outcome":"query","filters":{"city":"עיר שלא קיימת בשום קטלוג"}}""" };
        var service = CreateService(provider);

        var response = await service.GenerateAsync("test", CancellationToken.None);

        Assert.Equal("query", response.Status);
        Assert.Equal("עיר שלא קיימת בשום קטלוג", response.Filters?.City);
    }

    [Fact]
    public async Task Generate_includes_the_datasets_exact_property_types_in_the_prompt()
    {
        var provider = new FakeLlmProvider();
        var repository = new FakeDealRepository
        {
            Facts = new DatasetFacts { PropertyTypes = ["דירה", "פנטהאוז", "דופלקס"] }
        };
        var service = CreateService(provider, repository);

        await service.GenerateAsync("test", CancellationToken.None);

        Assert.Contains("דירה", provider.Request?.Prompt);
        Assert.Contains("פנטהאוז", provider.Request?.Prompt);
        Assert.Contains("דופלקס", provider.Request?.Prompt);
    }

    private static QueryGenerationService CreateService(FakeLlmProvider provider, FakeDealRepository? repository = null)
    {
        var options = Options.Create(new LlmOptions { Models = new LlmModelsOptions { QueryGeneration = "grok-test-model" } });
        repository ??= new FakeDealRepository();
        var queryService = new QueryService(repository);
        return new QueryGenerationService(provider, options, queryService, TestLocalityCatalog.CreateLoaded(), repository);
    }
}
