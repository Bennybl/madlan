namespace MadlanExplorer;

internal class NormalizedDate
{
    public NormalizedDate(DateOnly? start, DateOnly? end, string precision)
    {
        Start = start;
        End = end;
        Precision = precision;
    }

    public DateOnly? Start { get; }

    public DateOnly? End { get; }

    public string Precision { get; }
}
