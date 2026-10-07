using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

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
        builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.Configure<DatasetOptions>(builder.Configuration.GetSection(DatasetOptions.SectionName));
        builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection(LlmOptions.SectionName));
        builder.Services.Configure<MessagesOptions>(builder.Configuration.GetSection(MessagesOptions.SectionName));
        builder.Services.AddSingleton<IsraeliLocalityCatalog>();
        builder.Services.AddSingleton<DatasetStore>();
        builder.Services.AddSingleton<IDatasetMetadataProvider>(sp => sp.GetRequiredService<DatasetStore>());
        builder.Services.AddSingleton<IDealRepository, SqliteDealRepository>();
        builder.Services.AddSingleton<QueryService>();
        builder.Services.AddHttpClient<ILlmProvider, GrokLlmProvider>();
        builder.Services.AddSingleton<QueryOrchestrationService>();
        builder.Services.AddSingleton<MadlanApplicationService>();

        var app = builder.Build();
        var localityCatalog = app.Services.GetRequiredService<IsraeliLocalityCatalog>();
        var datasetStore = app.Services.GetRequiredService<DatasetStore>();
        var messages = app.Services.GetRequiredService<IOptions<MessagesOptions>>().Value;

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
                Message = messages.InternalError,
                RequestId = context.TraceIdentifier
            });
        }));

        app.MapGet("/healthz", () => Results.Ok(new HealthResponse("ok")));
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapControllers();

        return app;
    }
}

public record HealthResponse(string Status);
