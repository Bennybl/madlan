using MadlanExplorer;

namespace MadlanExplorer.Tests;

public class FakeDealRepository : IDealRepository
{
    public DealQuery? ReceivedQuery { get; private set; }
    public DealQueryResult Result { get; set; } = new() { TransactionCount = 7 };
    public DatasetFacts Facts { get; set; } = new();
    public DealDetail? Deal { get; set; }

    public List<DataQuery> ReceivedDataQueries { get; } = [];
    public DataQueryResult DataResult { get; set; } = new();
    public Func<DataQuery, DataQueryResult>? DataResultFactory { get; set; }

    public DealQueryResult Execute(DealQuery query)
    {
        ReceivedQuery = query;
        return Result;
    }

    public DataQueryResult ExecuteDataQuery(DataQuery query)
    {
        ReceivedDataQueries.Add(query);
        return DataResultFactory?.Invoke(query) ?? DataResult;
    }

    public DatasetFacts GetDatasetFacts()
    {
        return Facts;
    }

    public DealDetail? GetDeal(string dealId)
    {
        return Deal;
    }
}
