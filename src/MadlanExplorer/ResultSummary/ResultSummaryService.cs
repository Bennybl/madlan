using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MadlanExplorer;

public class ResultSummaryService
{
    private readonly ILlmProvider _provider;
    private readonly LlmOptions _options;

    public ResultSummaryService(ILlmProvider provider, IOptions<LlmOptions> options)
    {
        _provider = provider;
        _options = options.Value;
    }

    public async Task<ResultSummaryOutput> SummarizeAsync(
        string prompt,
        DealFilters filters,
        string datasetHash,
        DealQueryResult result,
        CancellationToken cancellationToken)
    {
        var model = _options.Models.ResultSummary;
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException("The result-summary model is not configured.");
        }

        var knownDealIds = CalculatedEvidence.KnownDealIds(result);

        var request = new LlmRequest
        {
            Stage = LlmStage.ResultSummary,
            Model = model,
            Prompt = BuildPrompt(prompt, filters, datasetHash, result)
        };

        var response = await _provider.CompleteAsync(request, cancellationToken);

        var serializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var output = JsonSerializer.Deserialize<ResultSummaryOutput>(response.Content, serializerOptions)
            ?? throw new InvalidOperationException("The summary model returned invalid JSON.");

        if (string.IsNullOrWhiteSpace(output.Summary))
        {
            throw new InvalidOperationException("The summary model returned an empty summary.");
        }

        if (output.ReferencedDealIds.Any(dealId => !knownDealIds.Contains(dealId)))
        {
            throw new InvalidOperationException("The summary model referenced a deal ID that is not part of the calculated evidence.");
        }

        if (result.TransactionCount > 0 && output.ReferencedDealIds.Count == 0)
        {
            throw new InvalidOperationException("The summary model did not reference any supporting evidence.");
        }

        if (result.TransactionCount == 0 && output.ReferencedDealIds.Count > 0)
        {
            throw new InvalidOperationException("The summary model referenced evidence that does not exist for an empty result.");
        }

        return output;
    }

    private static string BuildPrompt(string prompt, DealFilters filters, string datasetHash, DealQueryResult result)
    {
        var evidenceJson = CalculatedEvidence.ToJson(datasetHash, filters, result);

        return
            "You write a short Hebrew summary answering a real-estate question using only the calculated evidence given below. " +
            "Never invent a number, a deal ID, or a claim that is not present in the evidence. If there are no contributing transactions, say so plainly instead of guessing. " +
            "State the sample size and mention any warnings or exclusions in plain language. " +
            "When requestedMetric is present, it is the specific statistic the question asked for; requestedMetricValue is its computed value, and requestedMetricDealId (when present, for Min/Max metrics) is the specific matching deal to name in the answer. " +
            "Return JSON with summary (Hebrew text) and referencedDealIds (deal IDs from the evidence that support the summary; empty only when there is no evidence). " +
            $"Original user prompt: {prompt} " +
            $"Calculated evidence: {evidenceJson}";
    }
}
