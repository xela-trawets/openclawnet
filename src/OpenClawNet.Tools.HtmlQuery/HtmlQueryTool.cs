using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using AngleSharp;
using Microsoft.Extensions.Logging;
using OpenClawNet.Tools.Abstractions;

namespace OpenClawNet.Tools.HtmlQuery;

public sealed class HtmlQueryTool : ITool
{
    private static readonly HttpClient SharedHttp = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private readonly ILogger<HtmlQueryTool> _logger;

    public HtmlQueryTool(ILogger<HtmlQueryTool> logger)
    {
        _logger = logger;
    }

    public string Name => "html_query";

    public string Description =>
        "Fetch a URL and run a CSS selector against the parsed HTML (AngleSharp). " +
        "Returns the matched elements as text by default, or as attribute values when 'attribute' is set.";

    public ToolMetadata Metadata => new()
    {
        Name = Name,
        Description = Description,
        ParameterSchema = JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "url": { "type": "string", "description": "Absolute http/https URL to fetch." },
                "selector": { "type": "string", "description": "CSS selector, e.g. 'h1', 'a.title', 'meta[name=description]'." },
                "attribute": { "type": "string", "description": "Optional attribute name to extract instead of inner text (e.g. 'href', 'content')." },
                "limit": { "type": "integer", "description": "Maximum matches to return (default 10)." }
            },
            "required": ["url", "selector"]
        }
        """),
        RequiresApproval = false,
        Category = "web",
        Tags = ["html", "scrape", "css", "selector", "anglesharp"]
    };

    public async Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var url = input.GetStringArgument("url");
            var selector = input.GetStringArgument("selector");
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(selector))
                return ToolResult.Fail(Name, "'url' and 'selector' are required", sw.Elapsed);

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return ToolResult.Fail(Name, $"Invalid URL: {url}", sw.Elapsed);
            if (IsLocalUri(uri))
                return ToolResult.Fail(Name, "Fetching from local/private addresses is not allowed", sw.Elapsed);

            var attribute = input.GetStringArgument("attribute");
            var limit = Math.Clamp(input.GetArgument<int?>("limit") ?? 10, 1, 100);

            var http = SharedHttp;
            using var response = await http.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return ToolResult.Fail(Name, $"HTTP {(int)response.StatusCode} {response.StatusCode}", sw.Elapsed);
            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            var config = Configuration.Default;
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(req => req.Content(html), cancellationToken);
            var matches = document.QuerySelectorAll(selector!).Take(limit).ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"# {matches.Count} match(es) for selector: {selector}");
            sb.AppendLine($"URL: {url}");
            sb.AppendLine();
            foreach (var el in matches)
            {
                if (!string.IsNullOrEmpty(attribute))
                {
                    sb.AppendLine($"- {el.GetAttribute(attribute) ?? "(missing)"}");
                }
                else
                {
                    var text = el.TextContent?.Trim();
                    if (!string.IsNullOrEmpty(text))
                        sb.AppendLine($"- {text}");
                }
            }
            sw.Stop();
            return ToolResult.Ok(Name, sb.ToString(), sw.Elapsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HtmlQuery tool error");
            return ToolResult.Fail(Name, ex.Message, sw.Elapsed);
        }
    }

    private static bool IsLocalUri(Uri uri)
    {
        var host = uri.Host.ToLowerInvariant();
        return host == "localhost" || host == "127.0.0.1" || host == "::1" ||
               host.StartsWith("192.168.") || host.StartsWith("10.") || host.StartsWith("172.16.");
    }
}
