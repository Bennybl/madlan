using Microsoft.Extensions.Options;

namespace MadlanExplorer;

public class MadlanApplicationService
{
    private const string SummaryUnavailableMessage = "סיכום מאומת אינו זמין כעת. מוצגות התוצאות המחושבות בלבד.";

    private readonly QueryGenerationService _queryGenerationService;
    private readonly QueryVerificationService _queryVerificationService;
    private readonly QueryService _queryService;
    private readonly ResultSummaryService _resultSummaryService;
    private readonly ResultVerificationService _resultVerificationService;
    private readonly IDatasetMetadataProvider _datasetMetadataProvider;
    private readonly LlmOptions _options;

    public MadlanApplicationService(
        QueryGenerationService queryGenerationService,
        QueryVerificationService queryVerificationService,
        QueryService queryService,
        ResultSummaryService resultSummaryService,
        ResultVerificationService resultVerificationService,
        IDatasetMetadataProvider datasetMetadataProvider,
        IOptions<LlmOptions> options)
    {
        _queryGenerationService = queryGenerationService;
        _queryVerificationService = queryVerificationService;
        _queryService = queryService;
        _resultSummaryService = resultSummaryService;
        _resultVerificationService = resultVerificationService;
        _datasetMetadataProvider = datasetMetadataProvider;
        _options = options.Value;
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

        var verification = await _queryVerificationService.VerifyAsync(prompt, generated.Filters, workflowToken);
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
        var result = _queryService.Query(filters);
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
                summaryUnavailableMessage = SummaryUnavailableMessage;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            summaryUnavailableMessage = SummaryUnavailableMessage;
        }

        return new AskResponse
        {
            Prompt = prompt,
            Status = "query",
            Filters = filters,
            Result = result,
            DatasetHash = datasetHash,
            Summary = summary,
            Message = summaryUnavailableMessage
        };
    }
}
