namespace MadlanExplorer;

/// <summary>One iteration's response from the agent model: either request another data query, or conclude.</summary>
public class AgentAction
{
    public string Outcome { get; init; } = string.Empty;

    public DataQuerySpec? Query { get; init; }

    public string? Summary { get; init; }

    public IReadOnlyList<string>? ReferencedDealIds { get; init; }

    public string? Message { get; init; }
}
