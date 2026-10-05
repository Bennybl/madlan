using MadlanExplorer;

namespace MadlanExplorer.Tests;

public class FakeDealRepository : IDealRepository
{
    public DealQuery? ReceivedQuery { get; private set; }
    public DealQueryResult Result { get; set; } = new() { TransactionCount = 7 };

    public DealQueryResult Execute(DealQuery query)
    {
        ReceivedQuery = query;
        return Result;
    }

    public DatasetFacts GetDatasetFacts()
    {
        return new DatasetFacts();
    }

    public DealDetail? GetDeal(string dealId)
    {
        return null;
    }
}
