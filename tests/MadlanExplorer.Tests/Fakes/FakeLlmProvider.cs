using MadlanExplorer;

namespace MadlanExplorer.Tests;

public class FakeLlmProvider : ILlmProvider
{
    private readonly Dictionary<LlmStage, string> _contentByStage = [];

    public LlmRequest? Request { get; private set; }
    public List<LlmRequest> Requests { get; } = [];
    public string Content { get; set; } = """{"outcome":"query","filters":{"minimumRooms":4,"maximumRooms":4}}""";

    public void SetContent(LlmStage stage, string content)
    {
        _contentByStage[stage] = content;
    }

    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        Request = request;
        Requests.Add(request);
        var content = _contentByStage.TryGetValue(request.Stage, out var stageContent) ? stageContent : Content;
        return Task.FromResult(new LlmResponse { Content = content });
    }
}
