using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Extensions;
using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.Logging;
using OpenClawNet.Storage;
using OpenClawNet.Tools.Abstractions;

namespace OpenClawNet.Tools.Embeddings;

public sealed class EmbeddingsTool : ITool
{
    private readonly StorageOptions _storage;
    private readonly ILogger<EmbeddingsTool> _logger;
    private LocalEmbeddingGenerator? _generator;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public EmbeddingsTool(StorageOptions storage, ILogger<EmbeddingsTool> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    public string Name => "embeddings";

    public string Description =>
        "Generate a local text embedding (dense vector) and/or rank a list of candidates by " +
        "cosine similarity to a query. Powered by ElBruno.LocalEmbeddings (ONNX). " +
        "Use action='embed' to return the vector for a single string, or action='search' to rank candidates.";

    public ToolMetadata Metadata => new()
    {
        Name = Name,
        Description = Description,
        ParameterSchema = JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "action": { "type": "string", "enum": ["embed", "search"], "description": "embed: vector for one text. search: rank 'candidates' by similarity to 'text'." },
                "text": { "type": "string", "description": "Input text (or query for search)." },
                "candidates": { "type": "array", "items": { "type": "string" }, "description": "For search action: candidate strings to rank against 'text'." },
                "topK": { "type": "integer", "description": "Number of top results to return for search (default 5)." }
            },
            "required": ["action", "text"]
        }
        """),
        RequiresApproval = false,
        Category = "ai",
        Tags = ["embeddings", "ai", "rag", "semantic-search", "vector"]
    };

    public async Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var action = (input.GetStringArgument("action") ?? "embed").ToLowerInvariant();
            var text = input.GetStringArgument("text");
            if (string.IsNullOrWhiteSpace(text))
                return ToolResult.Fail(Name, "'text' is required", sw.Elapsed);

            var generator = await EnsureGeneratorAsync(cancellationToken);

            if (action == "search")
            {
                var candidates = input.GetArgument<string[]>("candidates") ?? Array.Empty<string>();
                if (candidates.Length == 0)
                    return ToolResult.Fail(Name, "'candidates' is required for action='search'", sw.Elapsed);
                var topK = input.GetArgument<int?>("topK") ?? 5;

                var corpus = await generator.GenerateAsync(candidates);
                var results = await generator.FindClosestAsync(text, candidates, corpus, topK: topK, minScore: 0f);
                sw.Stop();
                var sb = new StringBuilder();
                sb.AppendLine($"# Top {results.Count} matches for: \"{text}\"");
                foreach (var r in results)
                    sb.AppendLine($"- {r.Score:F4}  {r.Text}");
                return ToolResult.Ok(Name, sb.ToString(), sw.Elapsed);
            }

            var embedding = await generator.GenerateEmbeddingAsync(text);
            sw.Stop();
            var vec = embedding.Vector.ToArray();
            var preview = string.Join(", ", vec.Take(8).Select(f => f.ToString("F4")));
            return ToolResult.Ok(Name,
                $"# Embedding\nDimensions: {vec.Length}\nFirst 8: [{preview}, ...]",
                sw.Elapsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Embeddings tool error");
            return ToolResult.Fail(Name, ex.Message, sw.Elapsed);
        }
    }

    private async Task<LocalEmbeddingGenerator> EnsureGeneratorAsync(CancellationToken ct)
    {
        if (_generator is not null) return _generator;
        await _initLock.WaitAsync(ct);
        try
        {
            if (_generator is not null) return _generator;
            var cacheDir = Path.Combine(_storage.ModelsPath, "embeddings");
            Directory.CreateDirectory(cacheDir);
            var options = new LocalEmbeddingsOptions
            {
                CacheDirectory = cacheDir,
                EnsureModelDownloaded = true
            };
            _generator = new LocalEmbeddingGenerator(options);
            return _generator;
        }
        finally { _initLock.Release(); }
    }
}
