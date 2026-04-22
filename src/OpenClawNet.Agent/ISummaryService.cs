using OpenClawNet.Models.Abstractions;

namespace OpenClawNet.Agent;

/// <summary>
/// Summarizes long conversation histories to keep context windows manageable.
/// </summary>
public interface ISummaryService
{
    Task<string?> SummarizeIfNeededAsync(Guid sessionId, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default);
}
