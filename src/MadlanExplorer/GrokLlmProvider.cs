using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace MadlanExplorer;

public class GrokLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    public GrokLlmProvider(HttpClient httpClient, IOptions<LlmOptions> options)
    {
        var settings = options.Value;
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
        _httpClient.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
    }
    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync("chat/completions", new { model = request.Model, messages = new[] { new { role = "user", content = request.Prompt } }, response_format = new { type = "json_object" } }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var document = await response.Content.ReadFromJsonAsync<GrokResponse>(cancellationToken: cancellationToken) ?? throw new InvalidOperationException("Grok returned no response.");
        return new LlmResponse { Content = document.Choices.FirstOrDefault()?.Message?.Content ?? throw new InvalidOperationException("Grok returned no content.") };
    }
}
