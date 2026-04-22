using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using OpenClawNet.Mcp.Abstractions;
using OpenClawNet.Mcp.Core;
using OpenClawNet.Tools.Abstractions;
using OpenClawNet.Tools.Web;

namespace OpenClawNet.Mcp.Web;

/// <summary>
/// In-process MCP server wrapper for <see cref="WebTool"/>. The legacy tool exposed a single
/// operation <c>web_fetch</c>; this wrapper preserves both the operation set and the wire-form
/// name (<c>web_fetch</c> = server <c>web</c> + tool <c>fetch</c>).
/// </summary>
[McpServerToolType]
public sealed class WebMcpTools
{
    private readonly WebTool _tool;

    public WebMcpTools(WebTool tool)
    {
        _tool = tool;
    }

    [McpServerTool(Name = "fetch")]
    [Description("Fetch content from a URL and return it as text. Supports GET (default) and POST.")]
    public async Task<string> FetchAsync(
        [Description("The URL to fetch (http:// or https://)")] string url,
        [Description("HTTP method: GET (default) or POST")] string? method = null,
        [Description("Request body for POST requests")] string? body = null,
        CancellationToken cancellationToken = default)
    {
        var args = JsonSerializer.Serialize(new { url, method, body });
        var input = new ToolInput { ToolName = _tool.Name, RawArguments = args };
        var result = await _tool.ExecuteAsync(input, cancellationToken).ConfigureAwait(false);
        return result.Success ? result.Output : (result.Error ?? "fetch failed");
    }
}

/// <summary>Glue that registers <see cref="WebMcpTools"/> as the bundled <c>web</c> MCP server.</summary>
public sealed class WebBundledMcp : IBundledMcpServerRegistration
{
    // Stable, deterministic Id so restarts don't churn cached state.
    public static readonly Guid ServerId = new("8f7d1c80-1111-4a11-8001-77627e620001");

    public McpServerDefinition Definition { get; } = new()
    {
        Id = ServerId,
        Name = "web",
        Transport = McpTransport.InProcess,
        Enabled = true,
        IsBuiltIn = true,
    };

    public IReadOnlyList<McpServerTool> CreateTools(IServiceProvider services)
    {
        var instance = ActivatorUtilities.CreateInstance<WebMcpTools>(services);
        return BundledMcpToolFactory.CreateFor(instance);
    }
}


