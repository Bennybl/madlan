namespace MadlanExplorer;

public class DataQueryResult
{
    /// <summary>Total matching transactions under Filters, independent of grouping/aggregate -- always computed.</summary>
    public int TransactionCount { get; init; }

    public IReadOnlyList<DataRow> Rows { get; init; } = [];
}
