using System.Threading.Channels;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace OpenClawNet.Mcp.Core;

/// <summary>
/// Pair of <see cref="ITransport"/>s wired together by two in-memory channels.
/// One side is given to <see cref="ModelContextProtocol.Server.McpServer.Create"/>,
/// the other is wrapped in <see cref="InMemoryClientTransport"/> for the client.
/// </summary>
internal sealed class InMemoryDuplexTransport : ITransport
{
    private readonly ChannelWriter<JsonRpcMessage> _outgoing;
    private readonly ChannelReader<JsonRpcMessage> _incoming;
    private readonly string _name;
    private int _disposed;

    public InMemoryDuplexTransport(
        ChannelWriter<JsonRpcMessage> outgoing,
        ChannelReader<JsonRpcMessage> incoming,
        string name,
        string? sessionId = null)
    {
        _outgoing = outgoing;
        _incoming = incoming;
        _name = name;
        SessionId = sessionId;
    }

    public string? SessionId { get; }

    public ChannelReader<JsonRpcMessage> MessageReader => _incoming;

    public Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new InvalidOperationException($"Transport '{_name}' has been disposed.");

        return _outgoing.WriteAsync(message, cancellationToken).AsTask();
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            _outgoing.TryComplete();
        return default;
    }
}

/// <summary>
/// Hands an already-connected <see cref="ITransport"/> to <see cref="McpClient"/>.
/// Used by <see cref="InProcessMcpHost"/> to plug the in-memory transport pair
/// into the standard <c>McpClient.CreateAsync</c> entry point.
/// </summary>
internal sealed class InMemoryClientTransport : IClientTransport
{
    private readonly ITransport _clientSide;

    public InMemoryClientTransport(ITransport clientSide, string name)
    {
        _clientSide = clientSide;
        Name = name;
    }

    public string Name { get; }

    public Task<ITransport> ConnectAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_clientSide);
}

/// <summary>
/// Static factory that produces a back-to-back transport pair so an MCP server
/// and an MCP client can run in the same process with no IPC overhead.
/// </summary>
internal static class InMemoryTransportPair
{
    public static (ITransport ServerSide, IClientTransport ClientSide) Create(string serverName)
    {
        var clientToServer = Channel.CreateUnbounded<JsonRpcMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        var serverToClient = Channel.CreateUnbounded<JsonRpcMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        var serverSide = new InMemoryDuplexTransport(
            outgoing: serverToClient.Writer,
            incoming: clientToServer.Reader,
            name: $"{serverName}:server",
            sessionId: serverName);

        var clientPipe = new InMemoryDuplexTransport(
            outgoing: clientToServer.Writer,
            incoming: serverToClient.Reader,
            name: $"{serverName}:client",
            sessionId: serverName);

        return (serverSide, new InMemoryClientTransport(clientPipe, serverName));
    }
}
