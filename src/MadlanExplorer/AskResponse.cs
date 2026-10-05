namespace MadlanExplorer;

public class AskResponse
{
    public string Prompt { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? Message { get; init; }

    public DealFilters? Filters { get; init; }
}
