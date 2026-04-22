using OpenClawNet.Models.Abstractions;

namespace OpenClawNet.Agent;

/// <summary>
/// Orchestrates the full agent loop: prompt composition → model call → tool execution → response.
/// </summary>
public interface IAgentOrchestrator
{
    /// <summary>
    /// Processes an agent request within an existing session, persisting messages to the conversation store.
    /// </summary>
    Task<AgentResponse> ProcessAsync(AgentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes an agent request with streaming, yielding events in real time.
    /// </summary>
    IAsyncEnumerable<AgentStreamEvent> StreamAsync(AgentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes an agent request inside a clean, isolated session that shares no history with
    /// other sessions. Useful for background jobs, webhook handlers, and test harnesses.
    /// </summary>
    /// <param name="request">The agent request. The <see cref="AgentRequest.SessionId"/> is ignored;
    /// a fresh session ID is generated automatically.</param>
    /// <param name="options">Controls persistence, TTL, and optional workspace path override.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<AgentResponse> ProcessIsolatedAsync(AgentRequest request, IsolatedSessionOptions options, CancellationToken ct = default);
}
