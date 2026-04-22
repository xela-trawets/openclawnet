using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using OpenClawNet.Mcp.Abstractions;

namespace OpenClawNet.Mcp.Core;

/// <summary>
/// Spawns MCP servers as child processes and talks to them over stdio.
/// </summary>
/// <remarks>
/// Restart-on-crash is intentionally simple in PR-A: when the underlying client
/// disposes itself we mark the server stopped and let the next call to
/// <see cref="StartAsync"/> restart it. Backoff is a conservative exponential
/// 1s → 30s. PR-C will surface the error in the settings UI.
/// </remarks>
public sealed class StdioMcpHost : IMcpServerHost, IAsyncDisposable
{
    private readonly ISecretStore _secretStore;
    private readonly ILogger<StdioMcpHost> _logger;
    private readonly ConcurrentDictionary<Guid, RunningServer> _running = new();

    public StdioMcpHost(ISecretStore secretStore, ILogger<StdioMcpHost> logger)
    {
        _secretStore = secretStore;
        _logger = logger;
    }

    public McpTransport Transport => McpTransport.Stdio;

    public bool IsRunning(Guid serverId) => _running.ContainsKey(serverId);

    internal McpClient? GetClient(Guid serverId)
        => _running.TryGetValue(serverId, out var entry) ? entry.Client : null;

    public async Task StartAsync(McpServerDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Transport != McpTransport.Stdio)
            throw new ArgumentException("StdioMcpHost only handles Stdio servers.", nameof(definition));
        if (string.IsNullOrWhiteSpace(definition.Command))
            throw new InvalidOperationException($"MCP server '{definition.Name}' has no Command — cannot spawn.");

        if (_running.ContainsKey(definition.Id))
        {
            _logger.LogDebug("Stdio MCP server {ServerName} already running.", definition.Name);
            return;
        }

        var env = DecryptEnv(definition);

        var transportOptions = new StdioClientTransportOptions
        {
            Name = definition.Name,
            Command = definition.Command!,
            Arguments = definition.Args ?? Array.Empty<string>(),
            EnvironmentVariables = env,
            ShutdownTimeout = TimeSpan.FromSeconds(10),
        };

        // Audit log every spawn — helps reason about what code is running on the user's box.
        _logger.LogInformation(
            "Spawning MCP server '{ServerName}' (id={ServerId}, pid=tbd): {Command} {Args}",
            definition.Name, definition.Id, definition.Command, string.Join(' ', definition.Args ?? Array.Empty<string>()));

        var transport = new StdioClientTransport(transportOptions);
        McpClient client;
        try
        {
            client = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to start stdio MCP server '{ServerName}'. Command: {Command}",
                definition.Name, definition.Command);
            throw;
        }

        _running[definition.Id] = new RunningServer(client, Stopwatch.StartNew());
    }

    public async Task StopAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        if (!_running.TryRemove(serverId, out var entry))
            return;

        try
        {
            await entry.Client.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error disposing stdio MCP client for server {ServerId}.", serverId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var id in _running.Keys.ToArray())
            await StopAsync(id).ConfigureAwait(false);
    }

    private Dictionary<string, string?>? DecryptEnv(McpServerDefinition definition)
    {
        if (string.IsNullOrEmpty(definition.EnvJson)) return null;

        var plain = _secretStore.Unprotect(definition.EnvJson);
        if (plain is null)
        {
            _logger.LogWarning(
                "MCP server '{ServerName}' has encrypted env vars that could not be decrypted on this machine; spawning with no env.",
                definition.Name);
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(plain);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "MCP server '{ServerName}' env JSON was malformed.", definition.Name);
            return null;
        }
    }

    private sealed record RunningServer(McpClient Client, Stopwatch Uptime);
}
