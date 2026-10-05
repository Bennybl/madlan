using Microsoft.AspNetCore.Mvc;

namespace MadlanExplorer.Controllers;

[ApiController]
[Route("api/deals")]
public class DealsController : ControllerBase
{
    private readonly DatasetService _datasetService;

    public DealsController(DatasetService datasetService)
    {
        _datasetService = datasetService;
    }

    [HttpGet("{dealId}")]
    public ActionResult<DealDetail> Get(string dealId)
    {
        var deal = _datasetService.GetDeal(dealId);
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
