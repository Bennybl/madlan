namespace MadlanExplorer;

public class QueryGenerationOutput
{
    public string Outcome { get; init; } = string.Empty;

    public string? Message { get; init; }

    public DealFilters? Filters { get; init; }

    public IReadOnlyList<string>? Metrics { get; init; }

    public IReadOnlyList<RankedMetricSpec>? RankedMetrics { get; init; }

    public string? GroupBy { get; init; }
}
