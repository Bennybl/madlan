using System.Text.Json;
using System.Text.Json.Serialization;

namespace MadlanExplorer;

public static class CalculatedEvidence
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

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
            warnings = result.Warnings,
            requestedMetric = result.RequestedMetric,
            requestedMetricValue = result.RequestedMetricValue,
            requestedMetricDealId = result.RequestedMetricDealId
        };

        return JsonSerializer.Serialize(evidence, SerializerOptions);
    }

    public static HashSet<string> KnownDealIds(DealQueryResult result)
    {
        var dealIds = new HashSet<string>(
            result.ContributorDealIds
                .Concat(result.PriceContributorDealIds)
                .Concat(result.PricePerSqmContributorDealIds),
            StringComparer.Ordinal);

        if (result.RequestedMetricDealId is not null)
        {
            dealIds.Add(result.RequestedMetricDealId);
        }

        return dealIds;
    }
}
