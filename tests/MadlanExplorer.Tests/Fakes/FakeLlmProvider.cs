using MadlanExplorer;

namespace MadlanExplorer.Tests;

public class FakeLlmProvider : ILlmProvider
{
    private readonly Dictionary<LlmStage, string> _contentByStage = [];
    private readonly Dictionary<LlmStage, Exception> _exceptionByStage = [];

    public LlmRequest? Request { get; private set; }
    public List<LlmRequest> Requests { get; } = [];
    public string Content { get; set; } = """{"outcome":"query","filters":{"minimumRooms":4,"maximumRooms":4}}""";

    public void SetContent(LlmStage stage, string content)
    {
        _contentByStage[stage] = content;
    }

    public void SetException(LlmStage stage, Exception exception)
    {
        _exceptionByStage[stage] = exception;
    }

    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        Request = request;
        Requests.Add(request);

        if (_exceptionByStage.TryGetValue(request.Stage, out var exception))
        {
            throw exception;
        }

        var content = _contentByStage.TryGetValue(request.Stage, out var stageContent) ? stageContent : Content;
        return Task.FromResult(new LlmResponse { Content = content });
    }
}
