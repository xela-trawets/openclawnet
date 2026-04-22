namespace OpenClawNet.Mcp.Abstractions;

/// <summary>
/// Snapshot of a hosted MCP server's runtime state.
/// </summary>
public sealed record McpServerStatus(
    Guid ServerId,
    string Name,
    bool IsRunning,
    int ToolCount,
    string? LastError,
    DateTime? LastSeenUtc);
