using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenClawNet.Tools.Abstractions;

namespace OpenClawNet.Tools.Web;

public sealed class WebTool : ITool
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WebTool> _logger;
    private readonly WebToolOptions _options;

    public WebTool(HttpClient httpClient, ILogger<WebTool> logger, IOptions<WebToolOptions>? options = null)
    {
        _httpClient = httpClient;
        _options = options?.Value ?? new WebToolOptions();
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        _logger = logger;
    }
    
    public string Name => "web_fetch";
    public string Description => "Fetch content from a URL and return it as text.";
    
    public ToolMetadata Metadata => new()
    {
        Name = Name,
        Description = Description,
        ParameterSchema = JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "url": { "type": "string", "description": "The URL to fetch" },
                "method": { "type": "string", "enum": ["GET", "POST"], "description": "HTTP method (default: GET)" },
                "body": { "type": "string", "description": "Request body for POST requests" }
            },
            "required": ["url"]
        }
        """),
        RequiresApproval = true,
        Category = "web",
        Tags = ["web", "fetch", "http", "url"]
    };
    
    public async Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        
        try
        {
            var url = input.GetStringArgument("url");
            var method = input.GetStringArgument("method")?.ToUpperInvariant() ?? "GET";
            var body = input.GetStringArgument("body");
            
            if (string.IsNullOrEmpty(url))
            {
                return ToolResult.Fail(Name, "'url' is required", sw.Elapsed);
            }
            
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                return ToolResult.Fail(Name, $"Invalid URL: {url}. Only http and https are supported.", sw.Elapsed);
            }
            
            // Block local/private IPs for security
            if (IsLocalUri(uri))
            {
                return ToolResult.Fail(Name, "Fetching from local/private addresses is not allowed", sw.Elapsed);
            }
            
            _logger.LogInformation("Fetching URL: {Method} {Url}", method, url);
            
            HttpResponseMessage response;
            if (method == "POST" && body is not null)
            {
                response = await _httpClient.PostAsync(uri, new StringContent(body, System.Text.Encoding.UTF8, "application/json"), cancellationToken);
            }
            else
            {
                response = await _httpClient.GetAsync(uri, cancellationToken);
            }
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            
            // Truncate if too long
            if (content.Length > _options.MaxResponseLength)
            {
                content = content[.._options.MaxResponseLength] + $"\n\n... (truncated, total {content.Length} chars)";
            }
            
            sw.Stop();
            
            var result = $"HTTP {(int)response.StatusCode} {response.StatusCode}\nContent-Type: {response.Content.Headers.ContentType}\n\n{content}";
            
            _logger.LogInformation("Fetch complete: {StatusCode}, {Length} chars", (int)response.StatusCode, content.Length);
            
            return response.IsSuccessStatusCode
                ? ToolResult.Ok(Name, result, sw.Elapsed)
                : ToolResult.Fail(Name, result, sw.Elapsed);
        }
        catch (TaskCanceledException)
        {
            return ToolResult.Fail(Name, $"Request timed out after {_options.TimeoutSeconds}s", sw.Elapsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Web tool error");
            return ToolResult.Fail(Name, ex.Message, sw.Elapsed);
        }
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
