namespace OpenClawNet.Mcp.Abstractions;

/// <summary>
/// Per-tool overrides for an MCP server's tools. Persisted alongside the server definition.
/// Absence of an override means the server's defaults apply.
/// </summary>
public sealed class McpToolOverride
{
    /// <summary>Server this override belongs to.</summary>
    public Guid ServerId { get; set; }

    /// <summary>Bare tool name as advertised by the MCP server (no prefix).</summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// Per-tool approval override. <see langword="null"/> = inherit the server's default.
    /// </summary>
    public bool? RequireApproval { get; set; }

    /// <summary>
    /// When true, the tool is hidden from the agent regardless of the server's tool list.
    /// </summary>
    public bool Disabled { get; set; }
}
