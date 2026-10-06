namespace MadlanExplorer;

public class GroupedQueryResult
{
    public string GroupValue { get; init; } = string.Empty;

    public int TransactionCount { get; init; }

    public IReadOnlyList<RequestedMetricResult> RequestedMetrics { get; init; } = [];

    public IReadOnlyList<RankedMetricResult> RankedMetrics { get; init; } = [];
}
