namespace OpenClawNet.Agent;

/// <summary>
/// Internal runtime interface for agent orchestration.
/// Abstracts the agent execution loop and allows for future integration with frameworks like Microsoft Agent Framework
/// while keeping the solution's public IAgentOrchestrator interface stable.
/// 
/// This is an internal implementation detail and should not be exposed outside OpenClawNet.Agent.
/// IMPORTANT: This interface must be public (not internal) to support DI constructor injection,
/// but consumers should use IAgentOrchestrator instead.
/// </summary>
public interface IAgentRuntime
{
    /// <summary>
    /// Executes a single agent interaction within the given context.
    /// Handles prompt composition, model invocation, tool execution, and response generation.
    /// </summary>
    /// <param name="context">The agent interaction context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The completed agent context with final response and results.</returns>
    Task<AgentContext> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an agent interaction with streaming support.
    /// Yields events for real-time feedback during model invocation and tool execution.
    /// </summary>
    /// <param name="context">The agent interaction context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of agent stream events.</returns>
    IAsyncEnumerable<AgentStreamEvent> ExecuteStreamAsync(AgentContext context, CancellationToken cancellationToken = default);
}

