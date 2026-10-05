namespace MadlanExplorer;

public class QueryResponse
{
    public string DatasetHash { get; init; } = string.Empty;

    public DealFilters AppliedFilters { get; init; } = new();

    public DealQueryResult Result { get; init; } = new();
}
