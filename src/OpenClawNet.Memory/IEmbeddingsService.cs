namespace OpenClawNet.Memory;

/// <summary>
/// Provides local and cloud embeddings capabilities for semantic search and retrieval.
/// Supports multiple providers (local, Azure, Foundry) through abstraction.
/// </summary>
public interface IEmbeddingsService
{
    /// <summary>
    /// Gets the embedding provider name (e.g., "local-embeddings", "azure-openai", "foundry").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Generates an embedding vector for the given text.
    /// </summary>
    /// <param name="text">The text to embed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A vector representation of the text.</returns>
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates embeddings for multiple texts efficiently.
    /// </summary>
    /// <param name="texts">The texts to embed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of vector representations.</returns>
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates cosine similarity between two embedding vectors.
    /// </summary>
    /// <param name="vector1">First embedding vector.</param>
    /// <param name="vector2">Second embedding vector.</param>
    /// <returns>Similarity score between -1 and 1 (1 = identical, 0 = orthogonal, -1 = opposite).</returns>
    float CosineSimilarity(float[] vector1, float[] vector2);

    /// <summary>
    /// Checks if the embeddings service is available and operational.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the service is available, false otherwise.</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
