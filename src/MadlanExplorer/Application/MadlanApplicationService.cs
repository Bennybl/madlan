namespace MadlanExplorer;

public class MadlanApplicationService
{
    private readonly QueryGenerationService _queryGenerationService;
    private readonly QueryVerificationService _queryVerificationService;

    public MadlanApplicationService(
        QueryGenerationService queryGenerationService,
        QueryVerificationService queryVerificationService)
    {
        _queryGenerationService = queryGenerationService;
        _queryVerificationService = queryVerificationService;
    }

    public async Task<AskResponse> AskAsync(string prompt, CancellationToken cancellationToken)
    {
        var generated = await _queryGenerationService.GenerateAsync(prompt, cancellationToken);
        if (generated.Status != "query" || generated.Filters is null)
        {
            return generated;
        }

        var verification = await _queryVerificationService.VerifyAsync(prompt, generated.Filters, cancellationToken);
        if (verification.Outcome == "approved")
        {
            return generated;
        }

        return new AskResponse
        {
            Prompt = prompt,
            Status = "clarification",
            Message = verification.Message
        };
    }
}
