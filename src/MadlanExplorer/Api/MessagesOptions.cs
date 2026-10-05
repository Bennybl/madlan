namespace MadlanExplorer;

public class MessagesOptions
{
    public const string SectionName = "Messages";

    public string InternalError { get; set; } = string.Empty;

    public string InvalidPrompt { get; set; } = string.Empty;

    public string SummaryUnavailable { get; set; } = string.Empty;
}
