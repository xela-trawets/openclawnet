using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using OpenClawNet.Mcp.Abstractions;
using OpenClawNet.Mcp.Core;
using OpenClawNet.Tools.Abstractions;
using OpenClawNet.Tools.FileSystem;

namespace OpenClawNet.Mcp.FileSystem;

/// <summary>
/// In-process MCP server wrapper for <see cref="FileSystemTool"/>. The legacy tool multiplexed
/// 4 actions (read / write / list / find_projects) through a single <c>file_system</c> tool;
/// this wrapper exposes one MCP method per action.
/// </summary>
[McpServerToolType]
public sealed class FileSystemMcpTools
{
    private readonly FileSystemTool _tool;

    public FileSystemMcpTools(FileSystemTool tool)
    {
        _tool = tool;
    }

    [McpServerTool(Name = "read")]
    [Description("Read the contents of a file in the workspace. Files larger than 1MB are rejected.")]
    public Task<string> ReadAsync(
        [Description("File path (absolute, or relative to workspace root)")] string path,
        CancellationToken cancellationToken = default)
        => InvokeAsync("read", path, content: null, cancellationToken);

    [McpServerTool(Name = "write")]
    [Description("Write text content to a file in the workspace. Creates parent directories as needed.")]
    public Task<string> WriteAsync(
        [Description("File path (absolute, or relative to workspace root)")] string path,
        [Description("Content to write")] string content,
        CancellationToken cancellationToken = default)
        => InvokeAsync("write", path, content, cancellationToken);

    [McpServerTool(Name = "list")]
    [Description("List the contents of a directory in the workspace.")]
    public Task<string> ListAsync(
        [Description("Directory path (absolute, or relative to workspace root; '.' for the root)")] string path,
        CancellationToken cancellationToken = default)
        => InvokeAsync("list", path, content: null, cancellationToken);

    [McpServerTool(Name = "find_projects")]
    [Description("Find all .NET solution and project files (.sln/.slnx/.csproj/.fsproj) under a directory.")]
    public Task<string> FindProjectsAsync(
        [Description("Directory to search under")] string path,
        CancellationToken cancellationToken = default)
        => InvokeAsync("find_projects", path, content: null, cancellationToken);

    private async Task<string> InvokeAsync(string action, string path, string? content, CancellationToken ct)
    {
        var args = JsonSerializer.Serialize(new { action, path, content });
        var input = new ToolInput { ToolName = _tool.Name, RawArguments = args };
        var result = await _tool.ExecuteAsync(input, ct).ConfigureAwait(false);
        return result.Success ? result.Output : (result.Error ?? $"filesystem.{action} failed");
    }
}

/// <summary>Glue that registers <see cref="FileSystemMcpTools"/> as the bundled <c>filesystem</c> MCP server.</summary>
public sealed class FileSystemBundledMcp : IBundledMcpServerRegistration
{
    public static readonly Guid ServerId = new("8f7d1c80-1111-4a11-8001-77627e620004");

    public McpServerDefinition Definition { get; } = new()
    {
        Id = ServerId,
        Name = "filesystem",
        Transport = McpTransport.InProcess,
        Enabled = true,
        IsBuiltIn = true,
    };

    public IReadOnlyList<McpServerTool> CreateTools(IServiceProvider services)
    {
        var instance = ActivatorUtilities.CreateInstance<FileSystemMcpTools>(services);
        return BundledMcpToolFactory.CreateFor(instance);
    }
}
