using MadlanExplorer;

namespace MadlanExplorer.Tests;

public class FakeLlmProvider : ILlmProvider
{
    public LlmRequest? Request { get; private set; }
    public string Content { get; set; } = """{"outcome":"query","filters":{"minimumRooms":4,"maximumRooms":4}}""";

    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        Request = request;
        return Task.FromResult(new LlmResponse { Content = Content });
    }
}
