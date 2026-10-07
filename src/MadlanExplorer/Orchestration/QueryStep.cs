namespace MadlanExplorer;

/// <summary>One executed step of the agent loop, kept for transparency (the UI shows these) and for grounding the final answer.</summary>
public class QueryStep
{
    public DataQuery Query { get; init; } = new();

    public DataQueryResult Result { get; init; } = new();
}
