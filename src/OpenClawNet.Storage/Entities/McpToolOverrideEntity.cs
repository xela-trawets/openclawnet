namespace OpenClawNet.Storage.Entities;

/// <summary>
/// EF Core entity for <see cref="Mcp.Abstractions.McpToolOverride"/>.
/// Composite key (ServerId, ToolName).
/// </summary>
public class McpToolOverrideEntity
{
    public Guid ServerId { get; set; }
    public string ToolName { get; set; } = string.Empty;

    /// <summary><see langword="null"/> = inherit the server's default policy.</summary>
    public bool? RequireApproval { get; set; }

    public bool Disabled { get; set; }
    public DateTime UpdatedAt { get; set; }
}
