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
            requestedMetrics = result.RequestedMetrics.Select(m => new { metric = m.Metric, value = m.Value, dealId = m.DealId }),
            rankedMetrics = result.RankedMetrics.Select(m => new { metric = m.Metric, rank = m.Rank, value = m.Value, dealId = m.DealId }),
            groupBy = result.GroupBy,
            groups = result.Groups.Select(g => new
            {
                groupValue = g.GroupValue,
                transactionCount = g.TransactionCount,
                requestedMetrics = g.RequestedMetrics.Select(m => new { metric = m.Metric, value = m.Value, dealId = m.DealId }),
                rankedMetrics = g.RankedMetrics.Select(m => new { metric = m.Metric, rank = m.Rank, value = m.Value, dealId = m.DealId })
            })
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

        foreach (var metricResult in result.RequestedMetrics)
        {
            if (metricResult.DealId is not null)
            {
                dealIds.Add(metricResult.DealId);
            }
        }

        foreach (var rankedResult in result.RankedMetrics)
        {
            if (rankedResult.DealId is not null)
            {
                dealIds.Add(rankedResult.DealId);
            }
        }

        foreach (var group in result.Groups)
        {
            foreach (var metricResult in group.RequestedMetrics)
            {
                if (metricResult.DealId is not null)
                {
                    dealIds.Add(metricResult.DealId);
                }
            }

            foreach (var rankedResult in group.RankedMetrics)
            {
                if (rankedResult.DealId is not null)
                {
                    dealIds.Add(rankedResult.DealId);
                }
            }
        }

        return dealIds;
    }
}
