namespace MadlanExplorer;

public class OrchestrationResult
{
    public string Status { get; init; } = string.Empty;

    public string? Message { get; init; }

    public string? Summary { get; init; }

    public IReadOnlyList<QueryStep> Steps { get; init; } = [];
}
