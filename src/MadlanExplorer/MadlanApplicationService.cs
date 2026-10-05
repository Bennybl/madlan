namespace MadlanExplorer;

public class MadlanApplicationService
{
    public AskResponse Ask(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("A prompt is required.", nameof(prompt));
        }

        return new AskResponse
        {
            Prompt = prompt,
            Status = "received"
        };
    }
}
