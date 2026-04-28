namespace OpenClawNet.Agent;

/// <summary>
/// Represents a skill search result with ranking and confidence scores.
/// Used by semantic and keyword-based skill discovery mechanisms.
/// </summary>
public sealed record SkillResult
{
    /// <summary>
    /// The unique skill identifier or name.
    /// </summary>
    public required string SkillId { get; init; }

    /// <summary>
    /// Human-readable skill name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Skill description for context.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Keyword-based relevance score (0.0-1.0).
    /// </summary>
    public float KeywordScore { get; init; }

    /// <summary>
    /// Semantic (vector-based) relevance score (0.0-1.0). 
    /// Zero if semantic search unavailable.
    /// </summary>
    public float SemanticScore { get; init; }

    /// <summary>
    /// Final merged score combining keyword + semantic via RRF.
    /// </summary>
    public float FinalScore { get; init; }

    /// <summary>
    /// Confidence label: "HIGH" (>0.8), "MEDIUM" (0.5-0.8), "LOW" (<0.5).
    /// </summary>
    public string ConfidenceLevel { get; init; } = "MEDIUM";

    /// <summary>
    /// Source of the confidence score: "keyword_only", "semantic_reranked", "fallback".
    /// </summary>
    public string ConfidenceSource { get; init; } = "keyword_only";

    /// <summary>
    /// Original rank from keyword search (1-based).
    /// </summary>
    public int KeywordRank { get; init; }

    /// <summary>
    /// Rank after semantic re-ranking (1-based).
    /// </summary>
    public int FinalRank { get; init; }
}
