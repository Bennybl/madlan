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

    [HttpGet("dataset")]
    public ActionResult<DatasetResponse> GetDataset()
    {
        return Ok(_applicationService.GetDataset());
    }

    [HttpPost("query")]
    public ActionResult<QueryResponse> Query([FromBody] DealFilters? filters)
    {
        try
        {
            var response = _applicationService.Query(filters ?? new DealFilters());
            _logger.LogInformation(
                "Completed manual deal query {RequestId} with {TransactionCount} matching transactions",
                HttpContext.TraceIdentifier,
                response.Result.TransactionCount);
            return Ok(response);
        }
        catch (ArgumentException)
        {
            return BadRequest(new ApiErrorResponse
            {
                Code = "invalid_filters",
                Message = "המסננים שנשלחו אינם תקינים.",
                RequestId = HttpContext.TraceIdentifier
            });
        }
    }

    [HttpGet("deals/{dealId}")]
    public ActionResult<DealDetail> GetDeal(string dealId)
    {
        var deal = _applicationService.GetDeal(dealId);
        if (deal is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Code = "deal_not_found",
                Message = "העסקה המבוקשת לא נמצאה.",
                RequestId = HttpContext.TraceIdentifier
            });
        }

        return Ok(deal);
    }
}
