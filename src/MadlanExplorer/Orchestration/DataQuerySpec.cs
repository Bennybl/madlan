namespace MadlanExplorer;

/// <summary>JSON-facing shape of one data query, as the agent proposes it (string enum names for readability/robust parsing); converted to a real DataQuery after validation.</summary>
public class DataQuerySpec
{
    public DealFilters Filters { get; init; } = new();

    public string? GroupBy { get; init; }

    public string Aggregate { get; init; } = "count";

    public string? Field { get; init; }

    public int Rank { get; init; } = 1;

    public int? Limit { get; init; }

    public bool Descending { get; init; } = true;
}
