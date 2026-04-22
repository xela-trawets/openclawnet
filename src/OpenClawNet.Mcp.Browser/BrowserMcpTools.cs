using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using OpenClawNet.Mcp.Abstractions;
using OpenClawNet.Mcp.Core;
using OpenClawNet.Tools.Abstractions;
using OpenClawNet.Tools.Browser;

namespace OpenClawNet.Mcp.Browser;

/// <summary>
/// In-process MCP server wrapper for <see cref="BrowserTool"/>. The legacy tool multiplexed
/// 5 actions (navigate / extract-text / screenshot / click / fill) through a single
/// <c>browser</c> tool; this wrapper exposes one MCP method per action so the LLM gets
/// proper schemas (no invented actions, no dropped actions).
/// </summary>
[McpServerToolType]
public sealed class BrowserMcpTools
{
    private readonly BrowserTool _tool;

    public BrowserMcpTools(BrowserTool tool)
    {
        _tool = tool;
    }

    [McpServerTool(Name = "navigate")]
    [Description("Navigate the headless browser to a URL.")]
    public Task<string> NavigateAsync(
        [Description("URL to navigate to")] string url,
        CancellationToken cancellationToken = default)
        => InvokeAsync("navigate", url, selector: null, value: null, cancellationToken);

    [McpServerTool(Name = "extract_text")]
    [Description("Extract visible text from the current page or a specific element via CSS selector.")]
    public Task<string> ExtractTextAsync(
        [Description("URL to navigate to before extraction")] string url,
        [Description("Optional CSS selector to scope text extraction")] string? selector = null,
        CancellationToken cancellationToken = default)
        => InvokeAsync("extract-text", url, selector, value: null, cancellationToken);

    [McpServerTool(Name = "screenshot")]
    [Description("Take a screenshot of the page. Output is rendered by the browser-service " +
                 "(binary blobs are not transported over MCP — this returns a textual reference).")]
    public Task<string> ScreenshotAsync(
        [Description("URL to navigate to before screenshot")] string url,
        CancellationToken cancellationToken = default)
        => InvokeAsync("screenshot", url, selector: null, value: null, cancellationToken);

    [McpServerTool(Name = "click")]
    [Description("Click an element matched by a CSS selector.")]
    public Task<string> ClickAsync(
        [Description("URL to navigate to before clicking")] string url,
        [Description("CSS selector for the element to click")] string selector,
        CancellationToken cancellationToken = default)
        => InvokeAsync("click", url, selector, value: null, cancellationToken);

    [McpServerTool(Name = "fill")]
    [Description("Type text into an input element matched by a CSS selector.")]
    public Task<string> FillAsync(
        [Description("URL to navigate to before filling")] string url,
        [Description("CSS selector for the input element")] string selector,
        [Description("Text value to type into the input")] string value,
        CancellationToken cancellationToken = default)
        => InvokeAsync("fill", url, selector, value, cancellationToken);

    private async Task<string> InvokeAsync(string action, string url, string? selector, string? value, CancellationToken ct)
    {
        var args = JsonSerializer.Serialize(new { action, url, selector, value });
        var input = new ToolInput { ToolName = _tool.Name, RawArguments = args };
        var result = await _tool.ExecuteAsync(input, ct).ConfigureAwait(false);
        return result.Success ? result.Output : (result.Error ?? $"browser.{action} failed");
    }
}

/// <summary>Glue that registers <see cref="BrowserMcpTools"/> as the bundled <c>browser</c> MCP server.</summary>
public sealed class BrowserBundledMcp : IBundledMcpServerRegistration
{
    public static readonly Guid ServerId = new("8f7d1c80-1111-4a11-8001-77627e620003");

    public McpServerDefinition Definition { get; } = new()
    {
        Id = ServerId,
        Name = "browser",
        Transport = McpTransport.InProcess,
        Enabled = true,
        IsBuiltIn = true,
    };

    public IReadOnlyList<McpServerTool> CreateTools(IServiceProvider services)
    {
        var instance = ActivatorUtilities.CreateInstance<BrowserMcpTools>(services);
        return BundledMcpToolFactory.CreateFor(instance);
    }
}
