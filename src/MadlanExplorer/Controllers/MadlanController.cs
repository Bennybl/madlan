using Microsoft.AspNetCore.Mvc;

namespace MadlanExplorer.Controllers;

[ApiController]
[Route("api")]
public class MadlanController : ControllerBase
{
    private readonly MadlanApplicationService _applicationService;
    private readonly ILogger<MadlanController> _logger;

    public MadlanController(MadlanApplicationService applicationService, ILogger<MadlanController> logger)
    {
        _applicationService = applicationService;
        _logger = logger;
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
                Message = "יש לשלוח שאלה תקינה.",
                RequestId = HttpContext.TraceIdentifier
            });
        }
    }
}
