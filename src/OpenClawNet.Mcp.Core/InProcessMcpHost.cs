using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OpenClawNet.Mcp.Abstractions;

namespace OpenClawNet.Mcp.Core;

/// <summary>
/// Hosts MCP servers that live inside the OpenClawNet process. Each server is wired
/// to its client through an in-memory channel pair (no IPC, no subprocess).
/// </summary>
/// <remarks>
/// PR-A only registers the host — the actual built-in tool wrappers (Web/Shell/Browser/FileSystem)
/// land in PR-B. To publish a server in the meantime, callers register the server's tools via
/// <see cref="RegisterTools"/> before <see cref="StartAsync"/> runs.
/// </remarks>
public sealed class InProcessMcpHost : IMcpServerHost, IAsyncDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<InProcessMcpHost> _logger;
    private readonly ConcurrentDictionary<Guid, RunningServer> _running = new();
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<McpServerTool>> _toolRegistrations = new();

    public InProcessMcpHost(ILoggerFactory loggerFactory, ILogger<InProcessMcpHost> logger)
    {
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    public McpTransport Transport => McpTransport.InProcess;

    public bool IsRunning(Guid serverId) => _running.ContainsKey(serverId);

    /// <summary>
    /// Pre-register the tools an in-process server should expose. Must be called before
    /// <see cref="StartAsync"/> for that server. Callers in PR-B will use this to attach
    /// <c>[McpServerTool]</c>-annotated methods from the bundled tool wrappers.
    /// </summary>
    public void RegisterTools(Guid serverId, IEnumerable<McpServerTool> tools)
    {
        _toolRegistrations[serverId] = tools.ToList();
    }

    /// <summary>
    /// Returns a connected client for the named server, or <see langword="null"/> if it isn't running.
    /// Used by <see cref="McpToolProvider"/> to enumerate tools.
    /// </summary>
    internal McpClient? GetClient(Guid serverId)
        => _running.TryGetValue(serverId, out var entry) ? entry.Client : null;

    public async Task StartAsync(McpServerDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Transport != McpTransport.InProcess)
            throw new ArgumentException("InProcessMcpHost only handles InProcess servers.", nameof(definition));

        if (_running.ContainsKey(definition.Id))
        {
            _logger.LogDebug("In-process MCP server {ServerName} already running.", definition.Name);
            return;
        }

        var (serverTransport, clientTransport) = InMemoryTransportPair.Create(definition.Name);

        var serverOptions = new McpServerOptions
        {
            ServerInfo = new Implementation
            {
                Name = definition.Name,
                Version = "1.0.0",
            },
            ScopeRequests = false,
        };

        if (_toolRegistrations.TryGetValue(definition.Id, out var tools) && tools.Count > 0)
        {
            var collection = new McpServerPrimitiveCollection<McpServerTool>();
            foreach (var tool in tools)
                collection.Add(tool);

            serverOptions.ToolCollection = collection;
            serverOptions.Capabilities = new ServerCapabilities
            {
                Tools = new ToolsCapability(),
            };
        }
        else
        {
            // Empty tool set is valid — the server will respond to ListTools with [].
            serverOptions.ToolCollection = new McpServerPrimitiveCollection<McpServerTool>();
            serverOptions.Capabilities = new ServerCapabilities
            {
                Tools = new ToolsCapability(),
            };
        }

        var server = McpServer.Create(serverTransport, serverOptions, _loggerFactory);
        var serverRunCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var serverTask = Task.Run(() => server.RunAsync(serverRunCts.Token), serverRunCts.Token);

        McpClient client;
        try
        {
            client = await McpClient.CreateAsync(clientTransport, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            serverRunCts.Cancel();
            await server.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        _running[definition.Id] = new RunningServer(server, client, serverTask, serverRunCts);
        _logger.LogInformation("Started in-process MCP server '{ServerName}' ({ServerId}).", definition.Name, definition.Id);
    }

    public async Task StopAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        if (!_running.TryRemove(serverId, out var entry))
            return;

        try
        {
            await entry.Client.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _logger.LogDebug("MCP client for server {ServerId} dispose timed out; abandoning.", serverId);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error disposing MCP client for server {ServerId}.", serverId);
        }

        entry.ServerCts.Cancel();

        try
        {
            // Give the server a short window to honour the cancellation; if the in-memory
            // transport readers don't observe it (a known edge case), fall through rather
            // than block the host shutdown indefinitely.
            await entry.Server.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _logger.LogDebug("MCP server {ServerId} dispose timed out; abandoning.", serverId);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error disposing MCP server {ServerId}.", serverId);
        }

        entry.ServerCts.Dispose();
        _logger.LogInformation("Stopped in-process MCP server {ServerId}.", serverId);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var id in _running.Keys.ToArray())
            await StopAsync(id).ConfigureAwait(false);
    }

    private sealed record RunningServer(
        McpServer Server,
        McpClient Client,
        Task ServerLoop,
        CancellationTokenSource ServerCts);
}
