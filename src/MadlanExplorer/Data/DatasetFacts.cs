namespace MadlanExplorer;

public class DatasetFacts
{
    public int DealCount { get; init; }

    public int UsableDealCount { get; init; }

    public int ConflictingDealCount { get; init; }

    public IReadOnlyList<string> Cities { get; init; } = [];

    public IReadOnlyList<string> Neighborhoods { get; init; } = [];

    public IReadOnlyList<string> PropertyTypes { get; init; } = [];
}
