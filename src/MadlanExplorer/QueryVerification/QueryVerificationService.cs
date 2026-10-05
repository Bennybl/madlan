using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MadlanExplorer;

public class QueryVerificationService
{
    private readonly ILlmProvider _provider;
    private readonly LlmOptions _options;

    public QueryVerificationService(ILlmProvider provider, IOptions<LlmOptions> options)
    {
        _provider = provider;
        _options = options.Value;
    }

    public async Task<QueryVerificationOutput> VerifyAsync(string prompt, DealFilters filters, CancellationToken cancellationToken)
    {
        var model = _options.Models.QueryVerification;
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException("The query-verification model is not configured.");
        }

        var filtersJson = JsonSerializer.Serialize(filters);
        var verificationPrompt =
            "You check whether proposed structured filters correctly and completely capture a Hebrew real-estate question before they are executed. " +
            "Supported filters: city, neighborhood, propertyType, minimumRooms, maximumRooms, startDate, endDate (inclusive, ISO yyyy-MM-dd). " +
            "A specific room count means minimumRooms and maximumRooms are both set to that number; omitted filters impose no restriction and must not be invented. " +
            "Reject filters that omit a constraint stated in the prompt, use the wrong room bounds, name the wrong city or neighborhood, or use an incorrect date range. " +
            "Ask for clarification instead of rejecting when the prompt itself is ambiguous, such as a neighborhood that could refer to more than one place. " +
            "Return JSON with outcome (approved, rejected, clarification) and message explaining the reason when the outcome is not approved. " +
            $"Original user prompt: {prompt} " +
            $"Proposed filters: {filtersJson}";

        var request = new LlmRequest
        {
            Stage = LlmStage.QueryVerification,
            Model = model,
            Prompt = verificationPrompt
        };

        var response = await _provider.CompleteAsync(request, cancellationToken);

        var serializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var output = JsonSerializer.Deserialize<QueryVerificationOutput>(response.Content, serializerOptions)
            ?? throw new InvalidOperationException("The verification model returned invalid JSON.");

        if (output.Outcome is not ("approved" or "rejected" or "clarification"))
        {
            throw new InvalidOperationException("The verification model returned an unsupported outcome.");
        }

        return output;
    }
}
