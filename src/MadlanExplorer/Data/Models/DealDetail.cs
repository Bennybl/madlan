namespace MadlanExplorer;

public class DealDetail
{
    public string DealId { get; init; } = string.Empty;

    public string ConflictStatus { get; init; } = string.Empty;

    public int ReportCount { get; init; }

    public int DistinctReportCount { get; init; }

    public long? CanonicalReportId { get; init; }

    public IReadOnlyList<DealReportDetail> Reports { get; init; } = [];
}
