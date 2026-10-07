namespace MadlanExplorer;

/// <summary>
/// One generic, bounded data query: filter the sample, optionally group it by one discrete
/// dimension, and compute one aggregate over one numeric field. This single shape replaces the
/// earlier separate QueryMetric/RankedMetricRequest/GroupByField-breakdown/OutlierField
/// mechanisms -- an agent answering a compound question issues several of these in sequence
/// (see QueryOrchestrationService) instead of the schema growing a new special case per question
/// shape. Every query is still executed as fixed, parameterized SQL in SqliteDealRepository --
/// the model never writes SQL, it only picks values for these fields.
/// </summary>
public class DataQuery
{
    public DealFilters Filters { get; init; } = new();

    public GroupByField? GroupBy { get; init; }

    public DataAggregate Aggregate { get; init; } = DataAggregate.Count;

    public DataField? Field { get; init; }

    /// <summary>1 = the extreme value itself; 2 = the next one; and so on. Only meaningful for Min/Max.</summary>
    public int Rank { get; init; } = 1;

    /// <summary>When GroupBy is set, caps the number of groups returned, ordered by each group's computed value (see Descending). Unused otherwise.</summary>
    public int? Limit { get; init; }

    /// <summary>Order used when Limit narrows a grouped query to its top/bottom N groups.</summary>
    public bool Descending { get; init; } = true;
}
