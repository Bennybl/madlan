using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using MadlanExplorer;
using Xunit;

namespace MadlanExplorer.Tests;

public class AskApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AskApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Ask_preserves_the_original_natural_language_prompt()
    {
        const string prompt = "מה מחיר דירת ארבעה חדרים בחולון?";

        var response = await _client.PostAsJsonAsync("/api/ask", new AskRequest { Prompt = prompt });

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.Contains("X-Request-Id"));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(prompt, body.RootElement.GetProperty("prompt").GetString());
        Assert.Equal("received", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Ask_rejects_an_empty_prompt_with_a_hebrew_error()
    {
        var response = await _client.PostAsJsonAsync("/api/ask", new AskRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_prompt", body.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("message").GetString()));
    }
}
