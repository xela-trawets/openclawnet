namespace OpenClawNet.Mcp.Abstractions;

/// <summary>
/// Transport mechanism used to talk to an MCP server.
/// </summary>
public enum McpTransport
{
    /// <summary>
    /// Server runs in the same process; client and server are wired through
    /// an in-memory channel pair (no subprocess, no network). Used for the
    /// bundled tools shipped with OpenClawNet.
    /// </summary>
    InProcess = 0,

    /// <summary>
    /// Server runs as a child process and we talk over its stdin/stdout.
    /// This is the standard transport for user-installed MCP servers.
    /// </summary>
    Stdio = 1,

    /// <summary>
    /// Server is reachable over HTTP (Streamable HTTP / SSE).
    /// </summary>
    Http = 2,
}
