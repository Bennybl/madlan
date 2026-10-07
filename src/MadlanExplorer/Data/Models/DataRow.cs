namespace MadlanExplorer;

public class DataRow
{
    /// <summary>The distinct group-by value this row describes, or null for an ungrouped query.</summary>
    public string? GroupValue { get; init; }

    public decimal? Value { get; init; }

    /// <summary>The specific matching deal, present only for Min/Max and Outliers rows.</summary>
    public string? DealId { get; init; }

    /// <summary>Present only for Outliers rows: the interquartile fence Value fell outside of.</summary>
    public decimal? LowerBound { get; init; }

    public decimal? UpperBound { get; init; }
}
