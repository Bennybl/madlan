using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MadlanExplorer;

public class ResultVerificationService
{
    private readonly ILlmProvider _provider;
    private readonly LlmOptions _options;

    public ResultVerificationService(ILlmProvider provider, IOptions<LlmOptions> options)
    {
        _provider = provider;
        _options = options.Value;
    }

    public async Task<ResultVerificationOutput> VerifyAsync(
        string prompt,
        DealFilters filters,
        string datasetHash,
        DealQueryResult result,
        ResultSummaryOutput candidate,
        CancellationToken cancellationToken)
    {
        var model = _options.Models.ResultVerification;
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException("The result-verification model is not configured.");
        }

        var knownDealIds = CalculatedEvidence.KnownDealIds(result);
        if (candidate.ReferencedDealIds.Any(dealId => !knownDealIds.Contains(dealId)))
        {
            return new ResultVerificationOutput
            {
                Outcome = "rejected",
                Message = "The candidate summary references a deal ID that is not part of the calculated evidence."
            };
        }

        var request = new LlmRequest
        {
            Stage = LlmStage.ResultVerification,
            Model = model,
            Prompt = BuildPrompt(prompt, filters, datasetHash, result, candidate)
        };

        var response = await _provider.CompleteAsync(request, cancellationToken);

        var serializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var output = JsonSerializer.Deserialize<ResultVerificationOutput>(response.Content, serializerOptions)
            ?? throw new InvalidOperationException("The result-verification model returned invalid JSON.");

        if (output.Outcome is not ("approved" or "rejected"))
        {
            throw new InvalidOperationException("The result-verification model returned an unsupported outcome.");
        }

        return output;
    }

    private static string BuildPrompt(
        string prompt,
        DealFilters filters,
        string datasetHash,
        DealQueryResult result,
        ResultSummaryOutput candidate)
    {
        var evidenceJson = CalculatedEvidence.ToJson(datasetHash, filters, result);
        var candidateJson = JsonSerializer.Serialize(candidate);

        return
            "You check whether a candidate Hebrew summary correctly and completely answers the original question using only the calculated evidence given below. " +
            "Reject it if it answers a different question than the one asked, states a number or deal ID not present in the evidence, or omits a limitation the evidence requires, such as a warning, an exclusion, or an empty result. " +
            "Return JSON with outcome (approved, rejected) and message explaining the reason when the outcome is rejected. " +
            $"Original user prompt: {prompt} " +
            $"Calculated evidence: {evidenceJson} " +
            $"Candidate summary: {candidateJson}";
    }
}
