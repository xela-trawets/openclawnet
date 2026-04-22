using Microsoft.Extensions.Caching.Memory;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace OpenClawNet.Gateway.Services.Mcp;

/// <summary>
/// Loads and caches the curated suggestions list from <c>docs/mcp-suggestions.yaml</c>.
/// </summary>
/// <remarks>
/// The file ships next to the Gateway as a content asset (see csproj). We probe a few
/// well-known locations so this works under the dotnet test host, the published bin/, and
/// when running from source via Aspire.
/// </remarks>
public sealed class McpSuggestionsProvider
{
    private const string CacheKey = "mcp.suggestions.v1";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly IMemoryCache _cache;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<McpSuggestionsProvider> _logger;

    public McpSuggestionsProvider(
        IMemoryCache cache,
        IWebHostEnvironment env,
        ILogger<McpSuggestionsProvider> logger)
    {
        _cache = cache;
        _env = env;
        _logger = logger;
    }

    /// <summary>Optional override consulted before the on-disk file is read. Used by tests.</summary>
    public string? OverrideYamlContent { get; set; }

    public IReadOnlyList<McpSuggestion> GetAll()
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<McpSuggestion>? cached) && cached is not null)
            return cached;

        var yaml = OverrideYamlContent ?? ReadYamlFromDisk();
        var list = yaml is null ? Array.Empty<McpSuggestion>() : Parse(yaml);
        _cache.Set(CacheKey, (IReadOnlyList<McpSuggestion>)list, CacheTtl);
        return list;
    }

    public McpSuggestion? GetById(string id)
        => GetAll().FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<McpSuggestion> Parse(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var root = deserializer.Deserialize<SuggestionsRoot?>(yaml);
        return root?.Suggestions ?? new List<McpSuggestion>();
    }

    private string? ReadYamlFromDisk()
    {
        // Probe order: explicit env override → bin/docs/ (published) → repo docs/
        var candidates = new List<string>();

        var envOverride = Environment.GetEnvironmentVariable("OPENCLAWNET_MCP_SUGGESTIONS_PATH");
        if (!string.IsNullOrEmpty(envOverride))
            candidates.Add(envOverride);

        candidates.Add(Path.Combine(AppContext.BaseDirectory, "docs", "mcp-suggestions.yaml"));
        candidates.Add(Path.Combine(_env.ContentRootPath, "docs", "mcp-suggestions.yaml"));
        candidates.Add(Path.Combine(_env.ContentRootPath, "..", "..", "docs", "mcp-suggestions.yaml"));

        foreach (var path in candidates)
        {
            try
            {
                if (File.Exists(path))
                    return File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not read MCP suggestions from {Path}.", path);
            }
        }

        _logger.LogWarning("MCP suggestions YAML not found in any candidate location.");
        return null;
    }

    private sealed class SuggestionsRoot
    {
        public int Version { get; set; }
        public List<McpSuggestion>? Suggestions { get; set; }
    }
}
