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

    private static QueryGenerationService CreateService(FakeLlmProvider provider)
    {
        var options = Options.Create(new LlmOptions { Models = new LlmModelsOptions { QueryGeneration = "grok-test-model" } });
        return new QueryGenerationService(provider, options, new QueryService(new FakeDealRepository()));
    }
}
