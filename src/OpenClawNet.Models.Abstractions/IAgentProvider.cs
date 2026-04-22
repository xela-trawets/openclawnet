using Microsoft.Extensions.AI;

namespace OpenClawNet.Models.Abstractions;

/// <summary>
/// Creates MAF-compatible <see cref="IChatClient"/> instances for a specific LLM provider.
/// Phase 1 exposes <see cref="CreateChatClient"/> so the existing tool loop in
/// <c>DefaultAgentRuntime</c> can keep working while we migrate to the provider infrastructure.
/// </summary>
public interface IAgentProvider
{
    string ProviderName { get; }

    /// <summary>
    /// Creates an <see cref="IChatClient"/> configured for the given profile.
    /// </summary>
    IChatClient CreateChatClient(AgentProfile profile);

    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}
