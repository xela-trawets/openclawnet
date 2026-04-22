namespace OpenClawNet.Tools.Abstractions;

public interface IToolExecutor
{
    Task<ToolResult> ExecuteAsync(string toolName, string arguments, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ToolResult>> ExecuteBatchAsync(IReadOnlyList<(string ToolName, string Arguments)> calls, CancellationToken cancellationToken = default);
}
