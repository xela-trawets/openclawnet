namespace OpenClawNet.Models.Abstractions;

/// <summary>
/// Represents a communication channel through which users can interact with the agent
/// (e.g., Teams, web chat, Slack). Each channel has its own connection lifecycle
/// and message delivery mechanism.
/// </summary>
public interface IChannel
{
    /// <summary>
    /// Unique lowercase name for this channel.
    /// Conventional values: <c>"teams"</c>, <c>"webchat"</c>, <c>"slack"</c>.
    /// </summary>
    string ChannelName { get; }

    /// <summary>
    /// Whether this channel is currently enabled and accepting messages.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Sends a text message to a specific conversation on this channel.
    /// </summary>
    /// <param name="conversationId">The channel-specific conversation or thread identifier.</param>
    /// <param name="message">The message text to send.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendMessageAsync(string conversationId, string message, CancellationToken ct = default);

    /// <summary>
    /// Checks whether the channel's backing service is reachable and operational.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> if the channel is available; otherwise <see langword="false"/>.</returns>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}
