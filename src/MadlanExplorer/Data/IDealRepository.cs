namespace MadlanExplorer;

public interface IDealRepository
{
    DealQueryResult Execute(DealQuery query);

    DatasetFacts GetDatasetFacts();

    DealDetail? GetDeal(string dealId);
}
