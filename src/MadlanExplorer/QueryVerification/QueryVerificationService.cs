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

    public async Task<QueryVerificationOutput> VerifyAsync(string prompt, DealFilters filters, QueryMetric metric, CancellationToken cancellationToken)
    {
        var model = _options.Models.QueryVerification;
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException("The query-verification model is not configured.");
        }

        var filtersJson = JsonSerializer.Serialize(filters);
        var verificationPrompt =
            "You check whether a proposed structured filter and metric correctly and completely capture a Hebrew real-estate question before they are executed. " +
            "Supported filters: city, neighborhood, propertyType, minimumRooms, maximumRooms, startDate, endDate (inclusive, ISO yyyy-MM-dd), minimumFloor, maximumFloor, minimumYearBuilt, maximumYearBuilt, condition, source, hasElevator, hasParking, hasBalcony, hasSafeRoom. " +
            "A specific room count, floor, or year-built means the matching minimum and maximum filter are both set to that value; omitted filters impose no restriction and must not be invented. " +
            "The metric is one of TransactionCount, or Median/Average/Min/Max of Price, PricePerSqm, SizeSqm, Rooms, Floor, or YearBuilt. A cheapest/lowest-priced question needs MinPrice; most expensive needs MaxPrice; largest/smallest size needs MaxSizeSqm/MinSizeSqm; a typical or average value needs the matching Average metric; a plain \"how many\" needs TransactionCount. " +
            "Reject the proposal if it omits a constraint stated in the prompt, uses the wrong filter bounds, names the wrong city or neighborhood, uses an incorrect date range, or chose the wrong metric for what was asked. " +
            "Ask for clarification instead of rejecting when the prompt itself is ambiguous, such as a neighborhood that could refer to more than one place. " +
            "Return JSON with outcome (approved, rejected, clarification) and message (a short Hebrew explanation, required when the outcome is not approved). " +
            $"Original user prompt: {prompt} " +
            $"Proposed metric: {metric} " +
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
