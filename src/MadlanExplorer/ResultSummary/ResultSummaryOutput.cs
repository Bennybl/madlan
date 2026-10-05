namespace MadlanExplorer;

public class ResultSummaryOutput
{
    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> ReferencedDealIds { get; init; } = [];
}
