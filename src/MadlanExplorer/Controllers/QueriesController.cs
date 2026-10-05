using Microsoft.AspNetCore.Mvc;

namespace MadlanExplorer.Controllers;

[ApiController]
[Route("api/query")]
public class QueriesController : ControllerBase
{
    private readonly QueryService _queryService;
    private readonly DatasetStore _datasetStore;
    private readonly ILogger<QueriesController> _logger;

    public QueriesController(QueryService queryService, DatasetStore datasetStore, ILogger<QueriesController> logger)
    {
        _queryService = queryService;
        _datasetStore = datasetStore;
        _logger = logger;
    }

    [HttpPost]
    public ActionResult<QueryResponse> Post([FromBody] DealFilters? filters)
    {
        try
        {
            var appliedFilters = filters ?? new DealFilters();
            var result = _queryService.Query(appliedFilters);
            _logger.LogInformation(
                "Completed manual deal query {RequestId} with {TransactionCount} matching transactions",
                HttpContext.TraceIdentifier,
                result.TransactionCount);
            return Ok(new QueryResponse
            {
                DatasetHash = _datasetStore.Metadata.FileHash,
                AppliedFilters = appliedFilters,
                Result = result
            });
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
}
