namespace MadlanExplorer;

public class DealQuery
{
    public DealFilters Filters { get; init; } = new();

    public int EvidencePageSize { get; init; } = 100;

    public QueryMetric Metric { get; init; } = QueryMetric.TransactionCount;
}
