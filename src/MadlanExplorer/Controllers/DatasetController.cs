using Microsoft.AspNetCore.Mvc;

namespace MadlanExplorer.Controllers;

[ApiController]
[Route("api/dataset")]
public class DatasetController : ControllerBase
{
    private readonly DatasetService _datasetService;

    public DatasetController(DatasetService datasetService)
    {
        _datasetService = datasetService;
    }

    [HttpGet]
    public ActionResult<DatasetResponse> Get()
    {
        return Ok(_datasetService.GetDataset());
    }
}
