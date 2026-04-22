namespace OpenClawNet.Mcp.Abstractions;

/// <summary>
/// Hosts a single MCP server and exposes a way for the tool provider to talk to it.
/// One implementation per transport: in-process, stdio subprocess, HTTP.
/// </summary>
public interface IMcpServerHost
{
    /// <summary>Transport this host implementation handles.</summary>
    McpTransport Transport { get; }

    /// <summary>True once <see cref="StartAsync"/> has succeeded and the server is alive.</summary>
    bool IsRunning(Guid serverId);

    /// <summary>
    /// Start a server based on its definition. After this returns the
    /// <see cref="IMcpToolProvider"/> can reach the server through whatever
    /// internal connection the host owns.
    /// </summary>
    Task StartAsync(McpServerDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>Stop the named server and release its resources.</summary>
    Task StopAsync(Guid serverId, CancellationToken cancellationToken = default);
}
