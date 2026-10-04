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
        var app = builder.Build();

        app.MapGet("/healthz", () => Results.Ok(new HealthResponse("ok")));

        return app;
    }
}

public record HealthResponse(string Status);
