namespace MadlanExplorer;

public class AskResponse
{
    public string Prompt { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? Message { get; init; }

    public string? Summary { get; init; }

    public IReadOnlyList<QueryStep> Steps { get; init; } = [];

    public string? DatasetHash { get; init; }

    public string? DealId { get; init; }
}
