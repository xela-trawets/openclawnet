using Microsoft.Extensions.AI;

namespace OpenClawNet.Mcp.Abstractions;

/// <summary>
/// Aggregates tools from every running MCP server into a single <see cref="AITool"/>
/// catalog that <c>DefaultAgentRuntime</c> can hand straight to <c>ChatOptions.Tools</c>.
/// </summary>
/// <remarks>
/// Tool storage form is <c>&lt;serverPrefix&gt;.&lt;toolName&gt;</c>. The
/// LLM-wire form (returned by <see cref="AITool.Name"/>) uses an underscore
/// separator since most providers reject dots in function names.
/// </remarks>
public interface IMcpToolProvider
{
    /// <summary>
    /// Returns every visible tool from every running, enabled MCP server.
    /// Tools whose <see cref="McpToolOverride.Disabled"/> is set are filtered out.
    /// </summary>
    Task<IReadOnlyList<AITool>> GetAllToolsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns visible tools for a single server. Empty list if the server is unknown,
    /// disabled, or currently not running.
    /// </summary>
    Task<IReadOnlyList<AITool>> GetToolsForServerAsync(string serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops cached tool lists and re-queries every running server. Cheap to call.
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
