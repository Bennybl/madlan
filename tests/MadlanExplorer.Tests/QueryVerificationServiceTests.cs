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

        var result = await service.VerifyAsync(prompt, new DealFilters { MinimumRooms = 4, MaximumRooms = 4 }, CancellationToken.None);

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

        var result = await service.VerifyAsync("דירות בחולון", new DealFilters(), CancellationToken.None);

        Assert.Equal("rejected", result.Outcome);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task Verify_asks_for_clarification_on_an_ambiguous_neighborhood()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryVerification, """{"outcome":"clarification","message":"Which neighborhood did you mean?"}""");
        var service = CreateService(provider);

        var result = await service.VerifyAsync("דירות ברובע המרכזי", new DealFilters(), CancellationToken.None);

        Assert.Equal("clarification", result.Outcome);
        Assert.Equal("Which neighborhood did you mean?", result.Message);
    }

    [Fact]
    public async Task Verify_rejects_malformed_output()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryVerification, "not-json");
        var service = CreateService(provider);

        await Assert.ThrowsAsync<JsonException>(() => service.VerifyAsync("test", new DealFilters(), CancellationToken.None));
    }

    [Fact]
    public async Task Verify_rejects_an_unsupported_outcome_value()
    {
        var provider = new FakeLlmProvider();
        provider.SetContent(LlmStage.QueryVerification, """{"outcome":"maybe"}""");
        var service = CreateService(provider);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.VerifyAsync("test", new DealFilters(), CancellationToken.None));
    }

    [Fact]
    public async Task Verify_fails_clearly_when_the_verification_model_is_not_configured()
    {
        var provider = new FakeLlmProvider();
        var options = Options.Create(new LlmOptions());
        var service = new QueryVerificationService(provider, options);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.VerifyAsync("test", new DealFilters(), CancellationToken.None));
        Assert.Empty(provider.Requests);
    }

    private static QueryVerificationService CreateService(FakeLlmProvider provider)
    {
        var options = Options.Create(new LlmOptions { Models = new LlmModelsOptions { QueryVerification = "grok-verification-model" } });
        return new QueryVerificationService(provider, options);
    }
}
