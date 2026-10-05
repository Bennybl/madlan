namespace MadlanExplorer;

public class DatasetService
{
    private readonly DatasetStore _datasetStore;
    private readonly IDealRepository _dealRepository;

    public DatasetService(DatasetStore datasetStore, IDealRepository dealRepository)
    {
        _datasetStore = datasetStore;
        _dealRepository = dealRepository;
    }

    public DatasetResponse GetDataset()
    {
        return new DatasetResponse
        {
            FileHash = _datasetStore.Metadata.FileHash,
            ReportCount = _datasetStore.Metadata.ReportCount,
            Facts = _dealRepository.GetDatasetFacts()
        };
    }

    public DealDetail? GetDeal(string dealId)
    {
        return _dealRepository.GetDeal(dealId);
    }
}
