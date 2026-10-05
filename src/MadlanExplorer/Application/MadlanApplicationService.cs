namespace MadlanExplorer;

public class MadlanApplicationService
{
    private readonly QueryGenerationService _queryGenerationService;

    public MadlanApplicationService(QueryGenerationService queryGenerationService)
    {
        _queryGenerationService = queryGenerationService;
    }

    public Task<AskResponse> AskAsync(string prompt, CancellationToken cancellationToken)
    {
        return _queryGenerationService.GenerateAsync(prompt, cancellationToken);
    }
}
