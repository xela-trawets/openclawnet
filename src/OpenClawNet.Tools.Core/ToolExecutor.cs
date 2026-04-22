using Microsoft.Extensions.Logging;
using OpenClawNet.Tools.Abstractions;

namespace OpenClawNet.Tools.Core;

public sealed class ToolExecutor : IToolExecutor
{
    private readonly IToolRegistry _registry;
    private readonly IToolApprovalPolicy _approvalPolicy;
    private readonly ILogger<ToolExecutor> _logger;
    
    public ToolExecutor(IToolRegistry registry, IToolApprovalPolicy approvalPolicy, ILogger<ToolExecutor> logger)
    {
        _registry = registry;
        _approvalPolicy = approvalPolicy;
        _logger = logger;
    }
    
    public async Task<ToolResult> ExecuteAsync(string toolName, string arguments, CancellationToken cancellationToken = default)
    {
        var tool = _registry.GetTool(toolName);
        if (tool is null)
        {
            return ToolResult.Fail(toolName, $"Tool '{toolName}' not found", TimeSpan.Zero);
        }
        
        // Check approval
        if (await _approvalPolicy.RequiresApprovalAsync(toolName, arguments) &&
            !await _approvalPolicy.IsApprovedAsync(toolName, arguments))
        {
            return ToolResult.Fail(toolName, $"Tool '{toolName}' requires approval", TimeSpan.Zero);
        }
        
        var input = new ToolInput { ToolName = toolName, RawArguments = arguments };
        
        _logger.LogInformation("Executing tool {ToolName} with arguments: {Args}", toolName, arguments);
        
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var result = await tool.ExecuteAsync(input, cancellationToken);
            sw.Stop();
            _logger.LogInformation("Tool {ToolName} completed in {Duration}ms: Success={Success}", toolName, sw.ElapsedMilliseconds, result.Success);
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Tool {ToolName} failed after {Duration}ms", toolName, sw.ElapsedMilliseconds);
            return ToolResult.Fail(toolName, ex.Message, sw.Elapsed);
        }
    }
    
    public async Task<IReadOnlyList<ToolResult>> ExecuteBatchAsync(IReadOnlyList<(string ToolName, string Arguments)> calls, CancellationToken cancellationToken = default)
    {
        var results = new List<ToolResult>();
        foreach (var (toolName, arguments) in calls)
        {
            results.Add(await ExecuteAsync(toolName, arguments, cancellationToken));
        }
        return results;
    }
}
