namespace MadlanExplorer;

public interface IDealRepository
{
    DealQueryResult Execute(DealQuery query);
}
