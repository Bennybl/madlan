using System.Text.Json;

namespace MadlanExplorer;

public static class CalculatedEvidence
{
    public static string ToJson(string datasetHash, DealFilters filters, DealQueryResult result)
    {
        var evidence = new
        {
            datasetHash,
            filters,
            transactionCount = result.TransactionCount,
            medianPriceNis = result.MedianPriceNis,
            medianPricePerSqm = result.MedianPricePerSqm,
            priceContributorCount = result.PriceContributorCount,
            pricePerSqmContributorCount = result.PricePerSqmContributorCount,
            contributorDealIds = result.ContributorDealIds,
            hasMoreEvidence = result.HasMoreEvidence,
            exclusionReasons = result.ExclusionReasons,
            warnings = result.Warnings
        };

        return JsonSerializer.Serialize(evidence);
    }

    public static HashSet<string> KnownDealIds(DealQueryResult result)
    {
        return new HashSet<string>(
            result.ContributorDealIds
                .Concat(result.PriceContributorDealIds)
                .Concat(result.PricePerSqmContributorDealIds),
            StringComparer.Ordinal);
    }
}
