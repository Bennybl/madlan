namespace MadlanExplorer;

public class LlmRequest
{
    public LlmStage Stage { get; init; }

    public string Model { get; init; } = string.Empty;

    public string Prompt { get; init; } = string.Empty;
}
