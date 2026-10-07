using MadlanExplorer;

namespace MadlanExplorer.Tests;

public class FakeLlmProvider : ILlmProvider
{
    private readonly Dictionary<LlmStage, string> _contentByStage = [];
    private readonly Dictionary<LlmStage, Queue<string>> _contentSequenceByStage = [];
    private readonly Dictionary<LlmStage, Exception> _exceptionByStage = [];

    public LlmRequest? Request { get; private set; }
    public List<LlmRequest> Requests { get; } = [];
    public string Content { get; set; } = """{"outcome":"query","filters":{"minimumRooms":4,"maximumRooms":4}}""";

    public void SetContent(LlmStage stage, string content)
    {
        _contentByStage[stage] = content;
    }

    /// <summary>Queues one response per call to this stage, consumed in order -- for tests driving a multi-iteration loop where the same stage is called repeatedly with different intended responses each time.</summary>
    public void SetContentSequence(LlmStage stage, params string[] contents)
    {
        _contentSequenceByStage[stage] = new Queue<string>(contents);
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

        if (_contentSequenceByStage.TryGetValue(request.Stage, out var sequence) && sequence.Count > 0)
        {
            return Task.FromResult(new LlmResponse { Content = sequence.Dequeue() });
        }

        var content = _contentByStage.TryGetValue(request.Stage, out var stageContent) ? stageContent : Content;
        return Task.FromResult(new LlmResponse { Content = content });
    }
}
