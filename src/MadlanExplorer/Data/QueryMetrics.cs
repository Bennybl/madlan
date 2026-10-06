namespace MadlanExplorer;

public static class QueryMetrics
{
    /// <summary>
    /// Min/Max metrics identify one specific matching deal, so "rank" (1st, 2nd, 3rd...) is a
    /// well-defined extension of them (ORDER BY ... LIMIT 1 OFFSET rank-1). Average/Median/Count
    /// describe the whole sample, not a single deal, so ranking them has no meaning.
    /// </summary>
    public static readonly IReadOnlyList<QueryMetric> Rankable =
        Enum.GetValues<QueryMetric>()
            .Where(metric => metric.ToString().StartsWith("Min", StringComparison.Ordinal) || metric.ToString().StartsWith("Max", StringComparison.Ordinal))
            .ToList();
}
