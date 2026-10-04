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
        builder.Services.AddSingleton<DatasetStore>();

        var app = builder.Build();
        var datasetStore = app.Services.GetRequiredService<DatasetStore>();

        datasetStore.Load();
        app.Lifetime.ApplicationStopping.Register(datasetStore.Dispose);

        app.MapGet("/healthz", () => Results.Ok(new HealthResponse("ok")));

        return app;
    }
}

public record HealthResponse(string Status);
