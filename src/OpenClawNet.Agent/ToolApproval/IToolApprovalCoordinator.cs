namespace OpenClawNet.Agent.ToolApproval;

/// <summary>
/// Coordinates tool-call approval prompts between the agent runtime (the producer)
/// and the gateway HTTP endpoint (the consumer of user clicks).
///
/// Wave 4 design (Ripley + Bruno locked defaults 2026-04-19):
///   - The runtime calls <see cref="RequestApprovalAsync"/> with a fresh <see cref="Guid"/>
///     and awaits indefinitely (no timeout in v1).
///   - The gateway POST endpoint calls <see cref="TryResolve"/> when the user clicks.
///   - "Remember for this session" is stored in-memory keyed by conversation/session id.
/// </summary>
public interface IToolApprovalCoordinator
{
    /// <summary>
    /// Registers a pending approval request and asynchronously waits for the user's decision.
    /// </summary>
    Task<ApprovalDecision> RequestApprovalAsync(Guid requestId, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves a pending approval request with the user's decision.
    /// Returns false if the request id is unknown (already resolved, expired, or never registered).
    /// </summary>
    bool TryResolve(Guid requestId, ApprovalDecision decision);

    /// <summary>
    /// Records that the user has approved a tool name for the remainder of a conversation.
    /// </summary>
    void RememberApproval(Guid sessionId, string toolName);

    /// <summary>
    /// Returns true if the user has previously approved <paramref name="toolName"/>
    /// for this conversation with the "remember" flag set.
    /// </summary>
    bool IsToolApprovedForSession(Guid sessionId, string toolName);

    /// <summary>
    /// Clears every remembered approval for a conversation (used when the conversation ends or is reset).
    /// </summary>
    void ForgetSession(Guid sessionId);
}
