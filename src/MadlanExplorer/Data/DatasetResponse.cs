namespace MadlanExplorer;

public class DatasetResponse
{
    public string FileHash { get; init; } = string.Empty;

    public int ReportCount { get; init; }

    public DatasetFacts Facts { get; init; } = new();
}
