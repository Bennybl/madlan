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
            }),
            outlierFields = result.OutlierFields,
            outliers = result.Outliers.Select(o => new
            {
                field = o.Field,
                groupValue = o.GroupValue,
                dealId = o.DealId,
                value = o.Value,
                lowerBound = o.LowerBound,
                upperBound = o.UpperBound
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

        foreach (var outlier in result.Outliers)
        {
            dealIds.Add(outlier.DealId);
        }

        return dealIds;
    }

    /// <summary>
    /// True when the result has at least one metric that points at one specific deal (a Min/Max
    /// requested metric or a ranked metric, overall or within a group). A summary answering only
    /// aggregate statistics (count, average, median -- overall or per group) has no specific deal
    /// to name, so it has nothing meaningful to cite even though the underlying evidence is real.
    /// </summary>
    public static bool HasDealAnchoredMetric(DealQueryResult result)
    {
        if (result.RequestedMetrics.Any(m => m.DealId is not null) || result.RankedMetrics.Any(m => m.DealId is not null) || result.Outliers.Count > 0)
        {
            return true;
        }

        return result.Groups.Any(group =>
            group.RequestedMetrics.Any(m => m.DealId is not null) || group.RankedMetrics.Any(m => m.DealId is not null));
    }
}
