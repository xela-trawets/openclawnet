using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenClawNet.Tools.Abstractions;

namespace OpenClawNet.Tools.Browser;

public sealed class BrowserTool : ITool
{
    private readonly IHttpClientFactory _factory;
    private readonly ILogger<BrowserTool> _logger;

    public BrowserTool(IHttpClientFactory factory, ILogger<BrowserTool> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public string Name => "browser";
    public string Description => "Control a headless browser to navigate pages, extract text, take screenshots, and interact with web elements. Runs in an isolated browser service.";

    public ToolMetadata Metadata => new()
    {
        Name = Name,
        Description = Description,
        ParameterSchema = JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "action": { "type": "string", "enum": ["navigate", "extract-text", "screenshot", "click", "fill"], "description": "Browser action to perform" },
                "url": { "type": "string", "description": "URL to navigate to" },
                "selector": { "type": "string", "description": "CSS selector for the target element" },
                "value": { "type": "string", "description": "Text value to type into a field (fill action)" }
            },
            "required": ["action", "url"]
        }
        """),
        RequiresApproval = true,
        Category = "browser",
        Tags = ["browser", "web", "playwright", "automation", "scraping"]
    };

    public async Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var action = input.GetStringArgument("action");
            var url = input.GetStringArgument("url");
            var selector = input.GetStringArgument("selector");
            var value = input.GetStringArgument("value");

            _logger.LogInformation("Forwarding browser {Action} to browser-service: {Url}", action, url);
            var client = _factory.CreateClient("browser-service");
            var response = await client.PostAsJsonAsync("/api/browser/execute",
                new { action, url, selector, value }, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<BrowserServiceResponse>(cancellationToken: cancellationToken);
            sw.Stop();

            if (result is null) return ToolResult.Fail(Name, "Empty response from browser service", sw.Elapsed);
            return result.Success
                ? ToolResult.Ok(Name, result.Output, sw.Elapsed)
                : ToolResult.Fail(Name, result.Output, sw.Elapsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Browser tool proxy error");
            return ToolResult.Fail(Name, $"Browser service unavailable: {ex.Message}", sw.Elapsed);
        }
    }

    private sealed record BrowserServiceResponse(bool Success, string Output);
}
