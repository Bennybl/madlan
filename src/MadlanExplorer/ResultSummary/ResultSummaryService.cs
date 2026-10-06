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

        var referenceRequired = result.GroupBy is null || CalculatedEvidence.HasDealAnchoredMetric(result);
        if (result.TransactionCount > 0 && output.ReferencedDealIds.Count == 0 && referenceRequired)
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
            "Never make claims about the dataset's structure, fields, or this system's capabilities -- for example, never say a field like city \"is not segmented\" or \"does not exist\" in the data. You are not told the dataset's schema and have no basis for such a claim; the filters you are given are exactly what was applied to compute this evidence, nothing more, nothing less. Simply state the computed numbers and what they describe; do not explain, excuse, or editorialize about why the result covers what it covers. " +
            "State the sample size and mention any warnings or exclusions in plain Hebrew language for a non-technical reader -- never copy a warning or exclusion code into the summary verbatim. " +
            "Translate each warning code using exactly this glossary, word for word, and do not output the English code itself: " +
            "low_price_reported -> \"נמצאה עסקה עם מחיר מתחת ל-100,000 ₪; ייתכן שזהו טעות דיווח\"; " +
            "price_metric_has_fewer_than_five_contributors -> \"מחיר חציוני מבוסס על פחות מחמש עסקאות, מדגם קטן שיש להתייחס אליו בזהירות\"; " +
            "price_per_sqm_metric_has_fewer_than_five_contributors -> \"מחיר למ\\\"ר חציוני מבוסס על פחות מחמש עסקאות, מדגם קטן שיש להתייחס אליו בזהירות\"; " +
            "supplied_price_per_sqm_mismatch -> \"נמצאה עסקה שבה המחיר למ\\\"ר שדווח אינו תואם למחיר ולשטח שדווחו\". " +
            "requestedMetrics lists each specific statistic the question asked for beyond the always-present transaction count, median price and median price per square meter; mention every entry in it. Each entry's dealId (present only for Min/Max metrics) is the specific matching deal to name for that statistic. " +
            "rankedMetrics lists each Nth-highest/lowest statistic the question asked for (e.g. rank 2 of MaxPrice is \"the second most expensive price\"); mention every entry in it the same way, naming its rank in plain Hebrew (\"השני\", \"השלישי\" etc.) and its specific matching dealId. " +
            "When groupBy is set, the question asked for a per-category breakdown; the groups array has one entry per distinct value of that field (its groupValue), each with its own transactionCount, requestedMetrics and rankedMetrics computed only within that group. Cover every group in the summary -- for a short list of groups, name each one explicitly with its own numbers; for a long list, you may summarize the overall pattern but must still name the specific highest/lowest groups by their groupValue and dealId where relevant. Never claim a breakdown wasn't computed when groups is non-empty. Two groupValue entries that look similar (e.g. slightly different spellings or punctuation of what looks like the same city) are each a distinct, real, literal value actually present in the raw data -- this dataset has known unmerged spelling variants for some localities; never call this a duplicate or an error, just report each group's value and numbers as given. When every group's metrics are averages/medians only (no dealId on any of them), there is no single transaction to name for that breakdown, so referencedDealIds may be empty even though the summary is fully grounded in the computed per-group numbers -- never invent or guess a dealId just to have one to cite. " +
            "When outlierField and outliers are present, each entry is one specific deal whose value fell outside the typical range for its group (lowerBound to upperBound, the interquartile-range fence) -- name the specific deal(s), their value, and which group (if any) they belong to. If outliers is empty, say plainly that no outliers were found in the matching data, not that none exist in general. Never describe this as a formal test of whether the data follows a normal distribution -- it is a standard distance-based outlier rule, and you must not claim anything about normality one way or the other. " +
            "Return JSON with summary (Hebrew text) and referencedDealIds (deal IDs from the evidence that support the summary; empty only when there is no evidence). " +
            $"Original user prompt: {prompt} " +
            $"Calculated evidence: {evidenceJson}";
    }
}
