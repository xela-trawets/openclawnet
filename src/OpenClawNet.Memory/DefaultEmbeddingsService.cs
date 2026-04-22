using ElBruno.LocalEmbeddings.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;

namespace OpenClawNet.Memory;

/// <summary>
/// Local embeddings service backed by Elbruno.LocalEmbeddings.
/// Provides local-first semantic embeddings using ONNX models.
/// </summary>
public sealed class DefaultEmbeddingsService : IEmbeddingsService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly ILogger<DefaultEmbeddingsService> _logger;

    public DefaultEmbeddingsService(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        ILogger<DefaultEmbeddingsService> logger)
    {
        _embeddingGenerator = embeddingGenerator;
        _logger = logger;
    }

    public string ProviderName => "elbruno-local-embeddings";

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        _logger.LogDebug("Generating local embedding for text of length {Length}", text.Length);

        try
        {
            var generated = await _embeddingGenerator.GenerateAsync([text], cancellationToken: cancellationToken);
            if (generated.Count == 0)
                throw new InvalidOperationException("Embedding generator returned no vectors.");

            return generated[0].Vector.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding using Elbruno.LocalEmbeddings");
            throw;
        }
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);

        _logger.LogDebug("Generating embeddings for {Count} texts", texts.Count);

        if (texts.Count == 0)
            return Array.Empty<float[]>();

        var generated = await _embeddingGenerator.GenerateAsync(texts, cancellationToken: cancellationToken);
        var embeddings = generated.Select(e => e.Vector.ToArray()).ToList();

        return embeddings.AsReadOnly();
    }

    public float CosineSimilarity(float[] vector1, float[] vector2)
    {
        if (vector1.Length != vector2.Length)
            throw new ArgumentException("Vectors must have the same length");

        float dotProduct = 0f;
        float magnitude1 = 0f;
        float magnitude2 = 0f;

        for (int i = 0; i < vector1.Length; i++)
        {
            dotProduct += vector1[i] * vector2[i];
            magnitude1 += vector1[i] * vector1[i];
            magnitude2 += vector2[i] * vector2[i];
        }

        magnitude1 = MathF.Sqrt(magnitude1);
        magnitude2 = MathF.Sqrt(magnitude2);

        if (magnitude1 == 0 || magnitude2 == 0)
            return 0;

        return dotProduct / (magnitude1 * magnitude2);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var generated = await _embeddingGenerator.GenerateAsync(["health check"], cancellationToken: cancellationToken);
            return generated.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local embeddings service is not available");
            return false;
        }
    }
}

