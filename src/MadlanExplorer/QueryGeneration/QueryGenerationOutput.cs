namespace MadlanExplorer;

public class QueryGenerationOutput
{
    public string Outcome { get; init; } = string.Empty;

    public string? Message { get; init; }

    public DealFilters? Filters { get; init; }
}
