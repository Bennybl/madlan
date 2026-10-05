namespace MadlanExplorer;

public class ManualQueryResponse
{
    public DealFilters Filters { get; init; } = new();

    public DealQueryResult Result { get; init; } = new();

    public string DatasetHash { get; init; } = string.Empty;
}
