using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using OpenClawNet.Mcp.Abstractions;
using OpenClawNet.Mcp.Core;
using OpenClawNet.Tools.Abstractions;
using OpenClawNet.Tools.Shell;

namespace OpenClawNet.Mcp.Shell;

/// <summary>
/// In-process MCP server wrapper for <see cref="ShellTool"/>. The legacy single-method tool
/// (named <c>shell</c>) is exposed here as <c>shell.exec</c> (wire form <c>shell_exec</c>).
/// </summary>
[McpServerToolType]
public sealed class ShellMcpTools
{
    private readonly ShellTool _tool;

    public ShellMcpTools(ShellTool tool)
    {
        _tool = tool;
    }

    [McpServerTool(Name = "exec")]
    [Description("Execute a shell command via the isolated shell-service. Returns stdout/stderr.")]
    public async Task<string> ExecAsync(
        [Description("The shell command to execute")] string command,
        [Description("Working directory (optional)")] string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var args = JsonSerializer.Serialize(new { command, workingDirectory });
        var input = new ToolInput { ToolName = _tool.Name, RawArguments = args };
        var result = await _tool.ExecuteAsync(input, cancellationToken).ConfigureAwait(false);
        return result.Success ? result.Output : (result.Error ?? "shell exec failed");
    }
}

/// <summary>Glue that registers <see cref="ShellMcpTools"/> as the bundled <c>shell</c> MCP server.</summary>
public sealed class ShellBundledMcp : IBundledMcpServerRegistration
{
    public static readonly Guid ServerId = new("8f7d1c80-1111-4a11-8001-77627e620002");

    public McpServerDefinition Definition { get; } = new()
    {
        Id = ServerId,
        Name = "shell",
        Transport = McpTransport.InProcess,
        Enabled = true,
        IsBuiltIn = true,
    };

    public IReadOnlyList<McpServerTool> CreateTools(IServiceProvider services)
    {
        var instance = ActivatorUtilities.CreateInstance<ShellMcpTools>(services);
        return BundledMcpToolFactory.CreateFor(instance);
    }
}
