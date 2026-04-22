namespace OpenClawNet.Models.Abstractions;

/// <summary>
/// Registry for all <see cref="IChannel"/> implementations registered in the application.
/// Used to discover and route to channels by name.
/// </summary>
public interface IChannelRegistry
{
    /// <summary>
    /// Registers a channel. If a channel with the same <see cref="IChannel.ChannelName"/>
    /// already exists, it is replaced.
    /// </summary>
    /// <param name="channel">The channel to register.</param>
    void Register(IChannel channel);

    /// <summary>
    /// Returns the channel registered under <paramref name="name"/>,
    /// or <see langword="null"/> if no such channel exists.
    /// </summary>
    /// <param name="name">Case-insensitive channel name (e.g., <c>"teams"</c>).</param>
    IChannel? GetChannel(string name);

    /// <summary>
    /// Returns all currently registered channels.
    /// </summary>
    IReadOnlyList<IChannel> GetAllChannels();
}
