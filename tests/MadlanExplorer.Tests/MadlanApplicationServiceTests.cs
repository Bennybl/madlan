using Microsoft.Extensions.Options;
using MadlanExplorer;
using Xunit;

namespace MadlanExplorer.Tests;

public class MadlanApplicationServiceTests
{
    [Fact]
    public async Task Ask_returns_verified_filters_when_generation_and_verification_both_approve()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryGeneration, """{"outcome":"query","filters":{"minimumRooms":4,"maximumRooms":4}}""");
        provider.SetContent(LlmStage.QueryVerification, """{"outcome":"approved"}""");
        var service = CreateService(provider);
        const string prompt = "מה מחיר דירת ארבעה חדרים בחולון?";

        var response = await service.AskAsync(prompt, CancellationToken.None);

        Assert.Equal(prompt, response.Prompt);
        Assert.Equal("query", response.Status);
        Assert.Equal(4, response.Filters?.MinimumRooms);
        Assert.Equal(2, provider.Requests.Count);
        Assert.Equal(LlmStage.QueryGeneration, provider.Requests[0].Stage);
        Assert.Equal(LlmStage.QueryVerification, provider.Requests[1].Stage);
    }

    [Fact]
    public async Task Ask_returns_clarification_without_filters_when_verification_rejects_the_query()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryGeneration, """{"outcome":"query","filters":{"minimumRooms":4,"maximumRooms":4}}""");
        provider.SetContent(LlmStage.QueryVerification, """{"outcome":"rejected","message":"Room bounds do not match the prompt."}""");
        var service = CreateService(provider);

        var response = await service.AskAsync("test", CancellationToken.None);

        Assert.Equal("clarification", response.Status);
        Assert.Null(response.Filters);
        Assert.Equal("Room bounds do not match the prompt.", response.Message);
    }

    [Fact]
    public async Task Ask_returns_clarification_without_filters_when_verification_is_ambiguous()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryGeneration, """{"outcome":"query","filters":{"minimumRooms":4,"maximumRooms":4}}""");
        provider.SetContent(LlmStage.QueryVerification, """{"outcome":"clarification","message":"Which neighborhood did you mean?"}""");
        var service = CreateService(provider);

        var response = await service.AskAsync("test", CancellationToken.None);

        Assert.Equal("clarification", response.Status);
        Assert.Null(response.Filters);
        Assert.Equal("Which neighborhood did you mean?", response.Message);
    }

    [Fact]
    public async Task Ask_skips_verification_when_generation_already_asked_for_clarification()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryGeneration, """{"outcome":"clarification","message":"Which city did you mean?"}""");
        var service = CreateService(provider);

        var response = await service.AskAsync("test", CancellationToken.None);

        Assert.Equal("clarification", response.Status);
        Assert.Single(provider.Requests);
        Assert.Equal(LlmStage.QueryGeneration, provider.Requests[0].Stage);
    }

    [Fact]
    public async Task Ask_skips_verification_when_the_request_is_unsupported()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryGeneration, """{"outcome":"unsupported","message":"We cannot provide valuations."}""");
        var service = CreateService(provider);

        var response = await service.AskAsync("test", CancellationToken.None);

        Assert.Equal("unsupported", response.Status);
        Assert.Single(provider.Requests);
    }

    [Fact]
    public async Task Ask_never_returns_filters_when_the_verifier_is_unavailable()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryGeneration, """{"outcome":"query","filters":{"minimumRooms":4,"maximumRooms":4}}""");
        provider.SetContent(LlmStage.QueryVerification, "not-json");
        var service = CreateService(provider);

        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => service.AskAsync("test", CancellationToken.None));
    }

    private static MadlanApplicationService CreateService(FakeLlmProvider provider)
    {
        var options = Options.Create(new LlmOptions
        {
            Models = new LlmModelsOptions
            {
                QueryGeneration = "grok-generation-model",
                QueryVerification = "grok-verification-model"
            }
        });

        var queryService = new QueryService(new FakeDealRepository());
        var generationService = new QueryGenerationService(provider, options, queryService);
        var verificationService = new QueryVerificationService(provider, options);
        return new MadlanApplicationService(generationService, verificationService);
    }
}
