namespace OpenClawNet.Models.Abstractions;

/// <summary>
/// Core interface for LLM model providers (Ollama, Foundry Local, Azure OpenAI, Foundry).
/// </summary>
public interface IModelClient
{
    string ProviderName { get; }

    Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<ChatResponseChunk> StreamAsync(ChatRequest request, CancellationToken cancellationToken = default);

    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
