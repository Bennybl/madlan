namespace MadlanExplorer;

public class RequestedMetricResult
{
    public QueryMetric Metric { get; init; }

    public decimal? Value { get; init; }

    public string? DealId { get; init; }
}
