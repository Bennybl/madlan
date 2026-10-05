using MadlanExplorer;

namespace MadlanExplorer.Tests;

public class FakeDealRepository : IDealRepository
{
    public DealQuery? ReceivedQuery { get; private set; }

    public DealQueryResult Execute(DealQuery query)
    {
        ReceivedQuery = query;
        return new DealQueryResult { TransactionCount = 7 };
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
