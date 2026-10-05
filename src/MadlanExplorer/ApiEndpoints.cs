namespace MadlanExplorer;

public static class ApiEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/dataset", (DatasetService datasetService) => Results.Ok(datasetService.GetDataset()));

        app.MapPost("/api/query", (
            DealFilters? filters,
            QueryService queryService,
            DatasetStore datasetStore,
            HttpContext context,
            ILogger<Program> logger) =>
        {
            try
            {
                var appliedFilters = filters ?? new DealFilters();
                var result = queryService.Query(appliedFilters);
                logger.LogInformation(
                    "Completed manual deal query {RequestId} with {TransactionCount} matching transactions",
                    context.TraceIdentifier,
                    result.TransactionCount);
                return Results.Ok(new QueryResponse
                {
                    DatasetHash = datasetStore.Metadata.FileHash,
                    AppliedFilters = appliedFilters,
                    Result = result
                });
            }
            catch (ArgumentException)
            {
                return BadRequest("invalid_filters", "המסננים שנשלחו אינם תקינים.", context.TraceIdentifier);
            }
        });

        app.MapGet("/api/deals/{dealId}", (string dealId, DatasetService datasetService, HttpContext context) =>
        {
            var deal = datasetService.GetDeal(dealId);
            return deal is null
                ? NotFound("deal_not_found", "העסקה המבוקשת לא נמצאה.", context.TraceIdentifier)
                : Results.Ok(deal);
        });
    }

    public static IResult BadRequest(string code, string message, string requestId)
    {
        return Results.BadRequest(new ApiErrorResponse { Code = code, Message = message, RequestId = requestId });
    }

    public static IResult NotFound(string code, string message, string requestId)
    {
        return Results.NotFound(new ApiErrorResponse { Code = code, Message = message, RequestId = requestId });
    }
}
