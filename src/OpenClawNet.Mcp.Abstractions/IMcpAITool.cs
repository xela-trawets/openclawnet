namespace OpenClawNet.Mcp.Abstractions;

/// <summary>
/// Marker interface implemented by MCP-sourced <see cref="Microsoft.Extensions.AI.AITool"/>
/// wrappers. Exposes the storage-form name (<c>&lt;serverPrefix&gt;.&lt;toolName&gt;</c>)
/// in addition to the wire-form name (<c>&lt;serverPrefix&gt;_&lt;toolName&gt;</c>) used
/// over the LLM transport.
/// </summary>
/// <remarks>
/// Storage form is what <c>AgentProfile.EnabledTools</c> persists. The agent runtime
/// uses this to filter the tool catalog by an agent profile's allow-list.
/// </remarks>
public interface IMcpAITool
{
    /// <summary>Server-prefixed dotted name, e.g. <c>web.fetch</c>.</summary>
    string StorageName { get; }

    /// <summary>The owning server's stable id (<see cref="McpServerDefinition.Id"/>).</summary>
    System.Guid ServerId { get; }

    /// <summary>The owning server's display name (e.g. <c>web</c>).</summary>
    string ServerName { get; }
}
