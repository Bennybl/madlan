namespace MadlanExplorer;

public class RankedMetricResult
{
    public QueryMetric Metric { get; init; }

    public int Rank { get; init; }

    public decimal? Value { get; init; }

    public string? DealId { get; init; }
}
