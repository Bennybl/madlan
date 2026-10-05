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

        if (output.Outcome is not ("query" or "clarification" or "unsupported"))
        {
            throw new InvalidOperationException("The model returned an unsupported outcome.");
        }

        QueryMetric? metric = null;
        if (output.Outcome == "query")
        {
            if (output.Filters is not null)
            {
                _queryService.Validate(output.Filters);
            }

            metric = QueryMetric.TransactionCount;
            if (!string.IsNullOrWhiteSpace(output.Metric))
            {
                if (!Enum.TryParse(output.Metric, ignoreCase: true, out QueryMetric parsedMetric))
                {
                    throw new InvalidOperationException("The model returned an unsupported metric.");
                }

                metric = parsedMetric;
            }
        }

        return new AskResponse
        {
            Prompt = prompt,
            Status = output.Outcome,
            Message = output.Message,
            Filters = output.Filters,
            Metric = metric
        };
    }

    private static string BuildPrompt(string prompt)
    {
        var currentIsraelDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem")));
        var supportedMetrics = string.Join(", ", Enum.GetNames<QueryMetric>());

        return
            "You translate a Hebrew question about a historical sample of Israeli residential property deals into a structured filter and a metric to compute. " +
            "Supported filters: city, neighborhood, propertyType, minimumRooms, maximumRooms, startDate, endDate (inclusive, ISO yyyy-MM-dd), minimumFloor, maximumFloor, minimumYearBuilt, maximumYearBuilt, condition, source, and the booleans hasElevator, hasParking, hasBalcony, hasSafeRoom. " +
            "A specific room count, floor, or year-built means the matching minimum and maximum filter are both set to that value. Omitted filters impose no restriction; never invent a value the question did not state. " +
            "City and neighborhood names are resolved against an official locality catalog elsewhere in the system; pass through the name as given in the question. " +
            $"Also choose exactly one metric describing what to compute over the matching transactions, from this list only: {supportedMetrics}. " +
            "TransactionCount just counts matching transactions and is the default when the question has no specific statistic in mind, such as plain \"how many\". " +
            "The cheapest or lowest-priced matching transaction means MinPrice; the most expensive means MaxPrice; the largest or smallest apartment means MaxSizeSqm or MinSizeSqm; a typical or average value means the matching Average* metric; a middle or median value means the matching Median* metric. Price-per-square-meter, room-count, floor, and year-built have the same Min/Max/Average/Median options. " +
            "This system answers historical statistics over the supplied sample only, computed deterministically from these exact filters and this exact metric list; it never predicts a future price, appraises a specific named property, or computes anything outside this list. " +
            "A question asking for any of the statistics above, including one naming a specific past or current year, is supported and must produce outcome \"query\", never \"unsupported\". " +
            "Use outcome \"unsupported\" only for requests this system cannot do at all: future price predictions, property valuations or appraisals, investment advice, or a statistic outside the metric list above. " +
            "Use outcome \"clarification\" only when the question itself is genuinely ambiguous, such as a neighborhood name that could match more than one place, and explain in the message what additional detail is needed. " +
            $"The current date in Israel is {currentIsraelDate:yyyy-MM-dd}; resolve a relative date such as \"last year\" or \"this year\" against it. " +
            "Return JSON with outcome (query, clarification, unsupported), message (a short Hebrew explanation, required whenever outcome is not query), filters (required only when outcome is query), and metric (one exact name from the list above; omit it to mean TransactionCount). " +
            $"User prompt: {prompt}";
    }
}
