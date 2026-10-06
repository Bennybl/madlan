namespace MadlanExplorer;

public class AskResponse
{
    public string Prompt { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? Message { get; init; }

    public DealFilters? Filters { get; init; }

    public IReadOnlyList<QueryMetric>? Metrics { get; init; }

    public IReadOnlyList<RankedMetricRequest>? RankedMetrics { get; init; }

    public GroupByField? GroupBy { get; init; }

    public IReadOnlyList<OutlierField>? OutlierFields { get; init; }

    public DealQueryResult? Result { get; init; }

    public string? Summary { get; init; }

    public string? DatasetHash { get; init; }
}
