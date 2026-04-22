using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;

namespace OpenClawNet.Gateway.Services.Mcp;

/// <summary>
/// Typed HTTP client over the official MCP registry at
/// <c>https://registry.modelcontextprotocol.io/v0/servers</c>.
/// Caches each (query, cursor) pair for 10 minutes.
/// </summary>
/// <remarks>
/// Normalization rules:
/// <list type="bullet">
///   <item>If the registry entry has at least one <c>remotes[]</c> entry → HTTP transport with that URL.</item>
///   <item>Else if it has at least one <c>packages[]</c> entry with an npm registry → stdio transport
///         using <c>npx -y &lt;package&gt;</c>; pypi → <c>uvx &lt;package&gt;</c>.</item>
///   <item>Otherwise the entry is dropped from the normalized list (we have nothing actionable).</item>
/// </list>
/// </remarks>
public sealed class McpRegistryClient : IMcpRegistryClient
{
    public const string HttpClientName = "mcp-registry";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory _httpFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<McpRegistryClient> _logger;

    public McpRegistryClient(
        IHttpClientFactory httpFactory,
        IMemoryCache cache,
        ILogger<McpRegistryClient> logger)
    {
        _httpFactory = httpFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<McpRegistrySearchResult> SearchAsync(string? query, string? cursor, int limit, CancellationToken cancellationToken)
    {
        if (limit <= 0) limit = 20;
        if (limit > 100) limit = 100;

        var cacheKey = $"mcp.registry::{query}::{cursor}::{limit}";
        if (_cache.TryGetValue(cacheKey, out McpRegistrySearchResult? cached) && cached is not null)
            return cached;

        var client = _httpFactory.CreateClient(HttpClientName);

        var qs = new List<string> { $"limit={limit}" };
        if (!string.IsNullOrWhiteSpace(query)) qs.Add($"search={Uri.EscapeDataString(query)}");
        if (!string.IsNullOrWhiteSpace(cursor)) qs.Add($"cursor={Uri.EscapeDataString(cursor)}");
        var url = "v0/servers?" + string.Join('&', qs);

        RegistryListResponse? raw;
        try
        {
            raw = await client.GetFromJsonAsync<RegistryListResponse>(url, JsonOpts, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MCP registry search failed for query='{Query}'.", query);
            throw;
        }

        var normalized = (raw?.Servers ?? new List<RegistryListItem>())
            .Select(Normalize)
            .Where(e => e is not null)
            .Cast<McpRegistryEntry>()
            .ToList();

        var result = new McpRegistrySearchResult(normalized, raw?.Metadata?.NextCursor);
        _cache.Set(cacheKey, result, CacheTtl);
        return result;
    }

    private static McpRegistryEntry? Normalize(RegistryListItem item)
    {
        var server = item.Server;
        if (server is null) return null;

        var name = server.Name ?? string.Empty;
        if (string.IsNullOrEmpty(name)) return null;

        // 1. Prefer remote (HTTP) endpoints — they don't require local execution.
        var remote = server.Remotes?.FirstOrDefault(r => !string.IsNullOrEmpty(r?.Url));
        if (remote is not null)
        {
            return new McpRegistryEntry(
                Id: name,
                Name: name,
                Description: server.Description,
                Transport: "http",
                SuggestedCommand: null,
                SuggestedArgs: Array.Empty<string>(),
                SuggestedUrl: remote.Url);
        }

        // 2. Fall back to a runnable stdio package (npm/pypi).
        var pkg = server.Packages?.FirstOrDefault(p => !string.IsNullOrEmpty(p?.Name));
        if (pkg is not null)
        {
            var (cmd, args) = ToStdioCommand(pkg);
            if (cmd is not null)
            {
                return new McpRegistryEntry(
                    Id: name,
                    Name: name,
                    Description: server.Description,
                    Transport: "stdio",
                    SuggestedCommand: cmd,
                    SuggestedArgs: args,
                    SuggestedUrl: null);
            }
        }

        // Unknown shape — skip rather than show a broken row.
        return null;
    }

    private static (string? command, IReadOnlyList<string> args) ToStdioCommand(RegistryPackage pkg)
    {
        var registryName = (pkg.RegistryName ?? pkg.RegistryType ?? string.Empty).ToLowerInvariant();
        return registryName switch
        {
            "npm" => ("npx", new[] { "-y", pkg.Name! }),
            "pypi" => ("uvx", new[] { pkg.Name! }),
            "docker" or "oci" => ("docker", new[] { "run", "--rm", "-i", pkg.Name! }),
            _ => (null, Array.Empty<string>()),
        };
    }

    // --- Wire DTOs (subset of the registry's OpenAPI schema) ---------------------

    private sealed class RegistryListResponse
    {
        public List<RegistryListItem>? Servers { get; set; }
        public RegistryMetadata? Metadata { get; set; }
    }

    private sealed class RegistryMetadata
    {
        public string? NextCursor { get; set; }
        public int? Count { get; set; }
    }

    private sealed class RegistryListItem
    {
        public RegistryServer? Server { get; set; }
    }

    private sealed class RegistryServer
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Version { get; set; }
        public List<RegistryRemote>? Remotes { get; set; }
        public List<RegistryPackage>? Packages { get; set; }
    }

    private sealed class RegistryRemote
    {
        public string? Type { get; set; }
        public string? Url { get; set; }
    }

    private sealed class RegistryPackage
    {
        public string? RegistryName { get; set; }
        public string? RegistryType { get; set; }
        public string? Name { get; set; }
        public string? Version { get; set; }
    }
}

/// <summary>
/// Fallback used when the registry HTTP client cannot be constructed (e.g. tests
/// that don't want network calls). Always returns an empty result and logs a hint.
/// </summary>
public sealed class NullMcpRegistryClient : IMcpRegistryClient
{
    private readonly ILogger<NullMcpRegistryClient> _logger;

    public NullMcpRegistryClient(ILogger<NullMcpRegistryClient> logger) => _logger = logger;

    public Task<McpRegistrySearchResult> SearchAsync(string? query, string? cursor, int limit, CancellationToken cancellationToken)
    {
        _logger.LogInformation("MCP registry client is disabled; returning empty search result for query='{Query}'.", query);
        return Task.FromResult(new McpRegistrySearchResult(Array.Empty<McpRegistryEntry>(), null));
    }
}
