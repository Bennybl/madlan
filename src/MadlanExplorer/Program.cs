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
        builder.Services.AddSingleton<QueryService>();

        var app = builder.Build();
        var localityCatalog = app.Services.GetRequiredService<IsraeliLocalityCatalog>();
        var datasetStore = app.Services.GetRequiredService<DatasetStore>();

        localityCatalog.Load();
        datasetStore.Load();
        app.Lifetime.ApplicationStopping.Register(datasetStore.Dispose);

        app.MapGet("/healthz", () => Results.Ok(new HealthResponse("ok")));

        return app;
    }
}

public record HealthResponse(string Status);
