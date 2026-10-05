namespace MadlanExplorer;

public class Program
{
    public static void Main(string[] args)
    {
        var app = Application.Build(args);

        app.Run();
    }
}

public static class Application
{
    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.Configure<DatasetOptions>(builder.Configuration.GetSection(DatasetOptions.SectionName));
        builder.Services.AddSingleton<IsraeliLocalityCatalog>();
        builder.Services.AddSingleton<DatasetStore>();
        builder.Services.AddSingleton<IDealRepository, SqliteDealRepository>();
        builder.Services.AddSingleton<DatasetService>();
        builder.Services.AddSingleton<QueryService>();

        var app = builder.Build();
        var localityCatalog = app.Services.GetRequiredService<IsraeliLocalityCatalog>();
        var datasetStore = app.Services.GetRequiredService<DatasetStore>();

        localityCatalog.Load();
        datasetStore.Load();
        app.Lifetime.ApplicationStopping.Register(datasetStore.Dispose);

        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Request-Id"] = context.TraceIdentifier;
            await next(context);
        });

        app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new ApiErrorResponse
            {
                Code = "internal_error",
                Message = "אירעה שגיאה פנימית. נסו שוב מאוחר יותר.",
                RequestId = context.TraceIdentifier
            });
        }));

        app.MapGet("/healthz", () => Results.Ok(new HealthResponse("ok")));
        ApiEndpoints.Map(app);

        return app;
    }
}

public record HealthResponse(string Status);
