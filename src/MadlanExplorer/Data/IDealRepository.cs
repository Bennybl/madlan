namespace MadlanExplorer;

public interface IDealRepository
{
    DealQueryResult Execute(DealQuery query);

    DataQueryResult ExecuteDataQuery(DataQuery query);

    DatasetFacts GetDatasetFacts();

    DealDetail? GetDeal(string dealId);
}
