using System.Diagnostics;
using System.Text.Json;
using ElBruno.MarkItDotNet;
using Microsoft.Extensions.Logging;
using OpenClawNet.Tools.Abstractions;

namespace OpenClawNet.Tools.MarkItDown;

/// <summary>
/// Converts a URL (or other supported resource) into clean Markdown using
/// the ElBruno.MarkItDotNet library. Strips navigation/scripts/styles and
/// extracts the page title so agents can reason over readable content
/// instead of raw HTML.
/// </summary>
public sealed class MarkItDownTool : ITool
{
    private readonly MarkdownService _markdown;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<MarkItDownTool> _logger;

    public MarkItDownTool(
        MarkdownService markdown,
        IHttpClientFactory httpFactory,
        ILogger<MarkItDownTool> logger)
    {
        _markdown = markdown;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public string Name => "markdown_convert";

    public string Description =>
        "Convert a URL into clean Markdown (HTML stripped of navigation, scripts, and styles). " +
        "Use this instead of web_fetch when the agent needs readable content for summarization or RAG.";

    public ToolMetadata Metadata => new()
    {
        Name = Name,
        Description = Description,
        ParameterSchema = JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "url": { "type": "string", "description": "Absolute http/https URL to fetch and convert to Markdown" }
            },
            "required": ["url"]
        }
        """),
        RequiresApproval = false,
        Category = "web",
        Tags = ["markdown", "web", "convert", "rag", "summarize"]
    };

    public async Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var url = input.GetStringArgument("url");
            if (string.IsNullOrWhiteSpace(url))
                return ToolResult.Fail(Name, "'url' is required", sw.Elapsed);

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return ToolResult.Fail(Name, $"Invalid URL: {url}. Only http and https are supported.", sw.Elapsed);

            if (IsLocalUri(uri))
                return ToolResult.Fail(Name, "Fetching from local/private addresses is not allowed", sw.Elapsed);

            _logger.LogInformation("Converting URL to Markdown: {Url}", url);

            // Fetch with our managed HttpClient so timeouts/proxies/HSTS apply uniformly,
            // then hand the stream to MarkdownService which auto-detects HTML.
            var http = _httpFactory.CreateClient(nameof(MarkItDownTool));
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return ToolResult.Fail(Name, $"HTTP {(int)response.StatusCode} {response.StatusCode} when fetching {url}", sw.Elapsed);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var ext = ResolveExtension(response.Content.Headers.ContentType?.MediaType, uri);
            var result = await _markdown.ConvertAsync(stream, ext);
            sw.Stop();

            if (!result.Success)
                return ToolResult.Fail(Name, $"MarkItDotNet failed: {result.ErrorMessage ?? "unknown error"}", sw.Elapsed);

            var output = $"# Source: {url}\n# Format: {result.SourceFormat}\n\n{result.Markdown}";
            return ToolResult.Ok(Name, output, sw.Elapsed);
        }
        catch (TaskCanceledException)
        {
            return ToolResult.Fail(Name, "Request timed out", sw.Elapsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MarkItDown tool error");
            return ToolResult.Fail(Name, ex.Message, sw.Elapsed);
        }
    }

    private static string ResolveExtension(string? mediaType, Uri uri)
    {
        // Prefer Content-Type. Fall back to URL path extension. Default to .html.
        if (!string.IsNullOrEmpty(mediaType))
        {
            var mt = mediaType.ToLowerInvariant();
            if (mt.Contains("html")) return ".html";
            if (mt.Contains("pdf")) return ".pdf";
            if (mt.Contains("plain")) return ".txt";
            if (mt.Contains("markdown")) return ".md";
            if (mt.Contains("csv")) return ".csv";
            if (mt.Contains("json")) return ".json";
            if (mt.Contains("xml")) return ".xml";
        }
        var pathExt = Path.GetExtension(uri.AbsolutePath);
        return string.IsNullOrEmpty(pathExt) ? ".html" : pathExt.ToLowerInvariant();
    }

    private static bool IsLocalUri(Uri uri)
    {
        var host = uri.Host.ToLowerInvariant();
        return host == "localhost" ||
               host == "127.0.0.1" ||
               host == "::1" ||
               host.StartsWith("192.168.") ||
               host.StartsWith("10.") ||
               host.StartsWith("172.16.");
    }
}
