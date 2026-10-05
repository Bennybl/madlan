namespace MadlanExplorer;

public class MadlanApplicationService
{
    private readonly DatasetService _datasetService;
    private readonly QueryService _queryService;
    private readonly DatasetStore _datasetStore;

    public MadlanApplicationService(
        DatasetService datasetService,
        QueryService queryService,
        DatasetStore datasetStore)
    {
        _datasetService = datasetService;
        _queryService = queryService;
        _datasetStore = datasetStore;
    }

    public DatasetResponse GetDataset()
    {
        return _datasetService.GetDataset();
    }

    public QueryResponse Query(DealFilters filters)
    {
        return new QueryResponse
        {
            DatasetHash = _datasetStore.Metadata.FileHash,
            AppliedFilters = filters,
            Result = _queryService.Query(filters)
        };
    }

    public DealDetail? GetDeal(string dealId)
    {
        return _datasetService.GetDeal(dealId);
    }
}
