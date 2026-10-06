using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace MadlanExplorer;

public class MadlanApplicationService
{
    private static readonly Regex DealIdPattern = new(@"\bD\d{6}\b", RegexOptions.Compiled);

    private readonly QueryOrchestrationService _orchestrationService;
    private readonly QueryService _queryService;
    private readonly IDealRepository _dealRepository;
    private readonly IDatasetMetadataProvider _datasetMetadataProvider;
    private readonly LlmOptions _options;
    private readonly MessagesOptions _messages;

    public MadlanApplicationService(
        QueryOrchestrationService orchestrationService,
        QueryService queryService,
        IDealRepository dealRepository,
        IDatasetMetadataProvider datasetMetadataProvider,
        IOptions<LlmOptions> options,
        IOptions<MessagesOptions> messages)
    {
        _orchestrationService = orchestrationService;
        _queryService = queryService;
        _dealRepository = dealRepository;
        _datasetMetadataProvider = datasetMetadataProvider;
        _options = options.Value;
        _messages = messages.Value;
    }

    public ManualQueryResponse Query(DealFilters filters)
    {
        var result = _queryService.Query(filters);
        return new ManualQueryResponse
        {
            Filters = filters,
            Result = result,
            DatasetHash = _datasetMetadataProvider.Metadata.FileHash
        };
    }

    public DatasetSummaryResponse GetDatasetSummary()
    {
        var facts = _dealRepository.GetDatasetFacts();
        var metadata = _datasetMetadataProvider.Metadata;
        return new DatasetSummaryResponse
        {
            DatasetHash = metadata.FileHash,
            ReportCount = metadata.ReportCount,
            DealCount = facts.DealCount,
            UsableDealCount = facts.UsableDealCount,
            ConflictingDealCount = facts.ConflictingDealCount,
            Cities = facts.Cities,
            Neighborhoods = facts.Neighborhoods,
            PropertyTypes = facts.PropertyTypes
        };
    }

    public DealDetail? GetDeal(string dealId)
    {
        return _dealRepository.GetDeal(dealId);
    }

    public async Task<AskResponse> AskAsync(string prompt, CancellationToken cancellationToken)
    {
        var dealIdMatch = DealIdPattern.Match(prompt);
        if (dealIdMatch.Success)
        {
            var dealId = dealIdMatch.Value;
            return _dealRepository.GetDeal(dealId) is not null
                ? new AskResponse { Prompt = prompt, Status = "deal", DealId = dealId }
                : new AskResponse { Prompt = prompt, Status = "unsupported", Message = _messages.DealNotFound };
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.ServerTimeoutSeconds));
        var result = await _orchestrationService.RunAsync(prompt, timeoutCts.Token);

        return new AskResponse
        {
            Prompt = prompt,
            Status = result.Status,
            Message = result.Message,
            Summary = result.Summary,
            Steps = result.Steps,
            DatasetHash = _datasetMetadataProvider.Metadata.FileHash
        };
    }
}
