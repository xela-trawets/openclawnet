using ModelContextProtocol.Server;
using OpenClawNet.Mcp.Abstractions;

namespace OpenClawNet.Mcp.Core;

/// <summary>
/// Marker for an in-process MCP server that ships with OpenClawNet (Web/Shell/Browser/FileSystem).
/// Bundled servers are registered transiently from code on every startup — they are NOT
/// persisted to the <c>McpServerDefinitions</c> table (that seed lands in PR-E).
/// </summary>
public interface IBundledMcpServerRegistration
{
    /// <summary>
    /// Definition surfaced to <see cref="IMcpServerCatalog"/> so <see cref="McpToolProvider"/>
    /// can enumerate this server's tools. The Id must be stable across restarts.
    /// </summary>
    McpServerDefinition Definition { get; }

    /// <summary>
    /// Builds the concrete <see cref="McpServerTool"/> instances exposed by this server.
    /// Called once per startup, after the IServiceProvider is built.
    /// </summary>
    IReadOnlyList<McpServerTool> CreateTools(IServiceProvider services);
}
