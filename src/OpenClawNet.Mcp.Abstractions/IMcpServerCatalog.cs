namespace OpenClawNet.Mcp.Abstractions;

/// <summary>
/// Read-only access to the persisted MCP server definitions and tool overrides.
/// Implemented by the storage layer so that <c>OpenClawNet.Mcp.Core</c> doesn't
/// take a dependency on EF Core or the database directly.
/// </summary>
public interface IMcpServerCatalog
{
    /// <summary>Returns every persisted server definition (enabled and disabled).</summary>
    Task<IReadOnlyList<McpServerDefinition>> GetServersAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns every persisted tool override.</summary>
    Task<IReadOnlyList<McpToolOverride>> GetOverridesAsync(CancellationToken cancellationToken = default);
}
