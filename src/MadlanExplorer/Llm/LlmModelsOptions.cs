namespace MadlanExplorer;

public class LlmModelsOptions
{
    /// <summary>Used for every loop iteration: deciding the next data query, or concluding with a final answer/clarification/unsupported.</summary>
    public string Agent { get; set; } = string.Empty;

    /// <summary>Used once, at the end, to sanity-check the finished answer against the gathered evidence.</summary>
    public string Verification { get; set; } = string.Empty;
}
