using Microsoft.Extensions.Options;

namespace MadlanExplorer;

public class MadlanApplicationService
{
    private readonly QueryGenerationService _queryGenerationService;
    private readonly QueryVerificationService _queryVerificationService;
    private readonly QueryService _queryService;
    private readonly ResultSummaryService _resultSummaryService;
    private readonly ResultVerificationService _resultVerificationService;
    private readonly IDealRepository _dealRepository;
    private readonly IDatasetMetadataProvider _datasetMetadataProvider;
    private readonly LlmOptions _options;
    private readonly MessagesOptions _messages;

    public MadlanApplicationService(
        QueryGenerationService queryGenerationService,
        QueryVerificationService queryVerificationService,
        QueryService queryService,
        ResultSummaryService resultSummaryService,
        ResultVerificationService resultVerificationService,
        IDealRepository dealRepository,
        IDatasetMetadataProvider datasetMetadataProvider,
        IOptions<LlmOptions> options,
        IOptions<MessagesOptions> messages)
    {
        _queryGenerationService = queryGenerationService;
        _queryVerificationService = queryVerificationService;
        _queryService = queryService;
        _resultSummaryService = resultSummaryService;
        _resultVerificationService = resultVerificationService;
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
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.ServerTimeoutSeconds));
        var workflowToken = timeoutCts.Token;

        var generated = await _queryGenerationService.GenerateAsync(prompt, workflowToken);
        if (generated.Status != "query" || generated.Filters is null)
        {
            return generated;
        }

        var metrics = generated.Metrics ?? [];
        var rankedMetrics = generated.RankedMetrics ?? [];
        var verification = await _queryVerificationService.VerifyAsync(prompt, generated.Filters, metrics, rankedMetrics, workflowToken);
        if (verification.Outcome != "approved")
        {
            return new AskResponse
            {
                Prompt = prompt,
                Status = "clarification",
                Message = verification.Message
            };
        }

        var filters = generated.Filters;
        var result = _queryService.Query(filters, metrics, rankedMetrics);
        var datasetHash = _datasetMetadataProvider.Metadata.FileHash;

        string? summary = null;
        string? summaryUnavailableMessage = null;
        try
        {
            var candidate = await _resultSummaryService.SummarizeAsync(prompt, filters, datasetHash, result, workflowToken);
            var resultVerification = await _resultVerificationService.VerifyAsync(prompt, filters, datasetHash, result, candidate, workflowToken);

            if (resultVerification.Outcome == "approved")
            {
                summary = candidate.Summary;
            }
            else
            {
                summaryUnavailableMessage = _messages.SummaryUnavailable;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            summaryUnavailableMessage = _messages.SummaryUnavailable;
        }

        return new AskResponse
        {
            Prompt = prompt,
            Status = "query",
            Filters = filters,
            Metrics = metrics,
            RankedMetrics = rankedMetrics,
            Result = result,
            DatasetHash = datasetHash,
            Summary = summary,
            Message = summaryUnavailableMessage
        };
    }
}
