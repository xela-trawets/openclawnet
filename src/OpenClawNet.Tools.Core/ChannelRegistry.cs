using System.Collections.Concurrent;
using OpenClawNet.Models.Abstractions;

namespace OpenClawNet.Tools.Core;

/// <summary>
/// Thread-safe, dictionary-backed implementation of <see cref="IChannelRegistry"/>.
/// Channel lookups are case-insensitive.
/// </summary>
public sealed class ChannelRegistry : IChannelRegistry
{
    private readonly ConcurrentDictionary<string, IChannel> _channels =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public void Register(IChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        _channels[channel.ChannelName] = channel;
    }

    /// <inheritdoc/>
    public IChannel? GetChannel(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _channels.TryGetValue(name, out var channel) ? channel : null;
    }

    /// <inheritdoc/>
    public IReadOnlyList<IChannel> GetAllChannels() =>
        _channels.Values.ToList().AsReadOnly();
}
