namespace MadlanExplorer;

public class OutlierResult
{
    public OutlierField Field { get; init; }

    public string? GroupValue { get; init; }

    public string DealId { get; init; } = string.Empty;

    public decimal Value { get; init; }

    public decimal LowerBound { get; init; }

    public decimal UpperBound { get; init; }
}
