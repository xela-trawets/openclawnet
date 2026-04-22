namespace OpenClawNet.Tools.Abstractions;

/// <summary>
/// Base interface for all tools the agent can invoke.
/// </summary>
public interface ITool
{
    string Name { get; }
    string Description { get; }
    ToolMetadata Metadata { get; }
    
    Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken cancellationToken = default);
}
