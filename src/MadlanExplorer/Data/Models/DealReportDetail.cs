namespace MadlanExplorer;

public class DealReportDetail
{
    public long ReportId { get; init; }

    public int SourceRowNumber { get; init; }

    public string RawJson { get; init; } = string.Empty;

    public string NormalizedJson { get; init; } = string.Empty;

    public string QualityFlagsJson { get; init; } = string.Empty;
}
