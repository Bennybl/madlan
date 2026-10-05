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
    public ActionResult<AskResponse> Ask([FromBody] AskRequest? request)
    {
        try
        {
            var response = _applicationService.Ask(request?.Prompt ?? string.Empty);
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
