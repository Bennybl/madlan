using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MadlanExplorer.Controllers;

[ApiController]
[Route("api")]
public class MadlanController : ControllerBase
{
    private readonly MadlanApplicationService _applicationService;
    private readonly ILogger<MadlanController> _logger;
    private readonly MessagesOptions _messages;

    public MadlanController(
        MadlanApplicationService applicationService,
        ILogger<MadlanController> logger,
        IOptions<MessagesOptions> messages)
    {
        _applicationService = applicationService;
        _logger = logger;
        _messages = messages.Value;
    }

    [HttpPost("ask")]
    public async Task<ActionResult<AskResponse>> Ask([FromBody] AskRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _applicationService.AskAsync(request?.Prompt ?? string.Empty, cancellationToken);
            _logger.LogInformation(
                "Received property question {RequestId}",
                HttpContext.TraceIdentifier);
            return Ok(response);
        }
        catch (ArgumentException)
        {
            return BadRequest(new ApiErrorResponse
            {
                Code = "invalid_prompt",
                Message = _messages.InvalidPrompt,
                RequestId = HttpContext.TraceIdentifier
            });
        }
    }
}
