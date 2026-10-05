using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MadlanExplorer;

public class QueryGenerationService
{
    private readonly ILlmProvider _provider;
    private readonly LlmOptions _options;
    private readonly QueryService _queryService;

    public QueryGenerationService(ILlmProvider provider, IOptions<LlmOptions> options, QueryService queryService)
    {
        _provider = provider;
        _options = options.Value;
        _queryService = queryService;
    }

    public async Task<AskResponse> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("A prompt is required.", nameof(prompt));
        }

        var model = _options.Models.QueryGeneration;
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException("The query-generation model is not configured.");
        }

        var request = new LlmRequest
        {
            Stage = LlmStage.QueryGeneration,
            Model = model,
            Prompt = BuildPrompt(prompt)
        };

        var response = await _provider.CompleteAsync(request, cancellationToken);

        var serializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var output = JsonSerializer.Deserialize<QueryGenerationOutput>(response.Content, serializerOptions)
            ?? throw new InvalidOperationException("The model returned invalid JSON.");

        if (output.Outcome == "query" && output.Filters is not null)
        {
            _queryService.Validate(output.Filters);
        }

        if (output.Outcome is not ("query" or "clarification" or "unsupported"))
        {
            throw new InvalidOperationException("The model returned an unsupported outcome.");
        }

        return new AskResponse
        {
            Prompt = prompt,
            Status = output.Outcome,
            Message = output.Message,
            Filters = output.Filters
        };
    }

    private static string BuildPrompt(string prompt)
    {
        var currentIsraelDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem")));

        return
            "You translate a Hebrew question about a historical sample of Israeli residential property deals into structured filters. " +
            "Supported filters: city, neighborhood, propertyType, minimumRooms, maximumRooms, startDate, endDate (inclusive, ISO yyyy-MM-dd). " +
            "A specific room count means minimumRooms and maximumRooms are both set to that number. Omitted filters impose no restriction; never invent a value the question did not state. " +
            "City and neighborhood names are resolved against an official locality catalog elsewhere in the system; pass through the name as given in the question. " +
            "This system answers historical statistics over the supplied sample only: transaction count, median price and median price per square meter, filtered by city, neighborhood, property type, room count and date range. " +
            "A question asking for such a historical statistic, including one naming a specific past or current year, is supported and must produce outcome \"query\", never \"unsupported\". " +
            "Use outcome \"unsupported\" only for requests this system cannot do at all: future price predictions, property valuations or appraisals, investment advice, or anything requiring data beyond the supplied historical sample. " +
            "Use outcome \"clarification\" only when the question itself is genuinely ambiguous, such as a neighborhood name that could match more than one place, and explain in the message what additional detail is needed. " +
            $"The current date in Israel is {currentIsraelDate:yyyy-MM-dd}; resolve a relative date such as \"last year\" or \"this year\" against it. " +
            "Return JSON with outcome (query, clarification, unsupported), message (a short Hebrew explanation, required whenever outcome is not query), and filters (required only when outcome is query). " +
            $"User prompt: {prompt}";
    }
}
