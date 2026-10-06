namespace MadlanExplorer;

public class RankedMetricRequest
{
    public QueryMetric Metric { get; init; }

    public int Rank { get; init; } = 1;
}
