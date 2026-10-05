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

    [HttpGet("dataset")]
    public ActionResult<DatasetSummaryResponse> Dataset()
    {
        return Ok(_applicationService.GetDatasetSummary());
    }

    [HttpPost("query")]
    public ActionResult<ManualQueryResponse> Query([FromBody] DealFilters? filters)
    {
        try
        {
            var response = _applicationService.Query(filters ?? new DealFilters());
            _logger.LogInformation(
                "Received manual filter query {RequestId}",
                HttpContext.TraceIdentifier);
            return Ok(response);
        }
        catch (ArgumentException)
        {
            return BadRequest(new ApiErrorResponse
            {
                Code = "invalid_filters",
                Message = _messages.InvalidFilters,
                RequestId = HttpContext.TraceIdentifier
            });
        }
    }

    [HttpGet("deals/{dealId}")]
    public ActionResult<DealDetail> Deal(string dealId)
    {
        var detail = _applicationService.GetDeal(dealId);
        if (detail is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Code = "deal_not_found",
                Message = _messages.DealNotFound,
                RequestId = HttpContext.TraceIdentifier
            });
        }

        return Ok(detail);
    }
}
