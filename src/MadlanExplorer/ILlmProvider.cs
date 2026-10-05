namespace MadlanExplorer;

public interface ILlmProvider { Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken); }
