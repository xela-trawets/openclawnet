using OpenClawNet.Storage.Entities;

namespace OpenClawNet.Storage;

public interface IConversationStore
{
    Task<ChatSession> CreateSessionAsync(string? title = null, CancellationToken cancellationToken = default);
    Task<ChatSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatSession>> ListSessionsAsync(CancellationToken cancellationToken = default);
    Task DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<int> DeleteSessionsBulkAsync(IEnumerable<Guid> sessionIds, CancellationToken cancellationToken = default);
    Task<ChatSession> UpdateSessionTitleAsync(Guid sessionId, string title, CancellationToken cancellationToken = default);
    
    Task<ChatMessageEntity> AddMessageAsync(Guid sessionId, string role, string content, string? name = null, string? toolCallId = null, string? toolCallsJson = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatMessageEntity>> GetMessagesAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all but the most recent <paramref name="keepRecentCount"/> messages for a session.
    /// This is used after context compaction to prevent the message history from growing unbounded.
    /// </summary>
    /// <param name="sessionId">The session whose old messages should be removed.</param>
    /// <param name="keepRecentCount">Number of recent messages to retain.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// The method is a no-op when fewer than <paramref name="keepRecentCount"/> messages exist,
    /// or when no session summary has been stored (safety check — never prune unsummarized history).
    /// </remarks>
    Task PruneOldMessagesAsync(Guid sessionId, int keepRecentCount, CancellationToken cancellationToken = default);
}
