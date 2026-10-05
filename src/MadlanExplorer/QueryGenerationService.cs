using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MadlanExplorer;

public class QueryGenerationService
{
    private readonly ILlmProvider _provider;
    private readonly LlmOptions _options;
    private readonly QueryService _queryService;
    public QueryGenerationService(ILlmProvider provider, IOptions<LlmOptions> options, QueryService queryService) { _provider = provider; _options = options.Value; _queryService = queryService; }
    public async Task<AskResponse> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(prompt)) throw new ArgumentException("A prompt is required.", nameof(prompt));
        var model = _options.Models.QueryGeneration;
        if (string.IsNullOrWhiteSpace(model)) throw new InvalidOperationException("The query-generation model is not configured.");
        var response = await _provider.CompleteAsync(new LlmRequest { Stage = LlmStage.QueryGeneration, Model = model, Prompt = "Return JSON with outcome (query, clarification, unsupported), message, and filters when outcome is query. User prompt: " + prompt }, cancellationToken);
        var output = JsonSerializer.Deserialize<QueryGenerationOutput>(response.Content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidOperationException("The model returned invalid JSON.");
        if (output.Outcome == "query" && output.Filters is not null) _queryService.Validate(output.Filters);
        if (output.Outcome is not ("query" or "clarification" or "unsupported")) throw new InvalidOperationException("The model returned an unsupported outcome.");
        return new AskResponse { Prompt = prompt, Status = output.Outcome, Message = output.Message, Filters = output.Filters };
    }
}
