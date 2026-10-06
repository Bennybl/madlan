namespace MadlanExplorer;

public class DealQuery
{
    public DealFilters Filters { get; init; } = new();

    public int EvidencePageSize { get; init; } = 100;

    public IReadOnlyList<QueryMetric> Metrics { get; init; } = [];

    public IReadOnlyList<RankedMetricRequest> RankedMetrics { get; init; } = [];

    public GroupByField? GroupBy { get; init; }

    public OutlierField? OutlierField { get; init; }
}
