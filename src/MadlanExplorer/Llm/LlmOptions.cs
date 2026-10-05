namespace MadlanExplorer;

public class LlmOptions
{
    public const string SectionName = "Llm";

    public string Provider { get; set; } = "Grok";

    public string BaseUrl { get; set; } = "https://api.x.ai/v1";

    public string ApiKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 15;

    public LlmModelsOptions Models { get; set; } = new();
}
