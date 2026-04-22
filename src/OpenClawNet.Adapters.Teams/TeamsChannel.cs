using Microsoft.Extensions.Logging;
using OpenClawNet.Models.Abstractions;

namespace OpenClawNet.Adapters.Teams;

/// <summary>
/// <see cref="IChannel"/> implementation for Microsoft Teams.
/// Registers the Teams channel in the <see cref="IChannelRegistry"/> so it is discoverable
/// alongside other channels (web chat, Slack, etc.) without hard-coding Teams-specific logic.
/// </summary>
/// <remarks>
/// Outbound message delivery (proactive messaging) is not yet implemented. This implementation
/// exposes the channel metadata and availability check; proactive send support can be added
/// by injecting <c>ITurnContextFactory</c> or the Bot Framework <c>ConnectorClient</c>.
/// </remarks>
public sealed class TeamsChannel : IChannel
{
    private readonly ILogger<TeamsChannel> _logger;

    /// <inheritdoc/>
    public string ChannelName => "teams";

    /// <inheritdoc/>
    public bool IsEnabled { get; }

    public TeamsChannel(bool isEnabled, ILogger<TeamsChannel> logger)
    {
        IsEnabled = isEnabled;
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Proactive messaging to Teams requires a stored conversation reference obtained during
    /// the initial turn. This stub logs the intent and returns immediately.
    /// </remarks>
    public Task SendMessageAsync(string conversationId, string message, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "TeamsChannel.SendMessageAsync: conversationId={ConversationId} (proactive messaging not yet implemented)",
            conversationId);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Teams availability depends on the Bot Framework webhook being registered and reachable.
    /// Returns <see langword="true"/> when the channel is enabled; the actual endpoint health
    /// is managed by the Bot Framework adapter.
    /// </remarks>
    public Task<bool> IsAvailableAsync(CancellationToken ct = default) =>
        Task.FromResult(IsEnabled);
}
