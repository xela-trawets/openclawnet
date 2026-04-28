using Microsoft.Extensions.Logging;
using OpenClawNet.Memory;

namespace OpenClawNet.Agent;

/// <summary>
/// Implements semantic re-ranking of keyword search results using RRF (Reciprocal Rank Fusion).
/// Provides graceful fallback to keyword-only ranking on timeout or embedding service unavailability.
/// </summary>
public sealed class SemanticSkillRanker
{
    private readonly IEmbeddingsService? _embeddingsService;
    private readonly ILogger<SemanticSkillRanker> _logger;
    private const int RrfK = 60; // RRF parameter for (1/(k+rank)) scoring
    private const int SemanticTimeoutMs = 100; // Hard deadline for semantic enrichment

    public SemanticSkillRanker(
        IEmbeddingsService? embeddingsService,
        ILogger<SemanticSkillRanker> logger)
    {
        _embeddingsService = embeddingsService;
        _logger = logger;
    }

    /// <summary>
    /// Re-ranks keyword search results using semantic scoring via RRF fusion.
    /// Takes top-N keyword results and applies semantic re-ranking within 100ms timeout.
    /// On timeout or service unavailability, returns keyword results unmodified.
    /// </summary>
    /// <param name="keywordResults">Top keyword search results with scores.</param>
    /// <param name="userQuery">Original user query for semantic embedding.</param>
    /// <param name="cancellationToken">Cancellation token for graceful degradation.</param>
    /// <returns>List of skills re-ranked with merged confidence scores.</returns>
    public async Task<List<SkillResult>> RerankAsync(
        IReadOnlyList<SkillResult> keywordResults,
        string userQuery,
        CancellationToken cancellationToken = default)
    {
        // Quick exit: empty results or no embedding service
        if (keywordResults.Count == 0)
            return [];

        if (_embeddingsService == null)
        {
            _logger.LogDebug("Embedding service unavailable; returning keyword-only results");
            return ApplyFallbackConfidence(keywordResults);
        }

        // Check service availability without timeout pressure
        try
        {
            var available = await _embeddingsService.IsAvailableAsync(
                CancellationToken.None);
            if (!available)
            {
                _logger.LogWarning("Embedding service reported unavailable; using keyword-only fallback");
                return ApplyFallbackConfidence(keywordResults);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check embedding service availability; using fallback");
            return ApplyFallbackConfidence(keywordResults);
        }

        // Enforce 100ms timeout for semantic re-ranking
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(SemanticTimeoutMs));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cts.Token, cancellationToken);

        try
        {
            var semanticResults = await PerformSemanticRerankAsync(
                keywordResults, userQuery, linkedCts.Token);
            return semanticResults;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Semantic re-rank exceeded 100ms timeout; returning keyword-only results");
            return ApplyFallbackConfidence(keywordResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Semantic re-rank failed unexpectedly; returning keyword-only results");
            return ApplyFallbackConfidence(keywordResults);
        }
    }

    /// <summary>
    /// Core semantic re-ranking logic using RRF fusion of keyword + semantic scores.
    /// </summary>
    private async Task<List<SkillResult>> PerformSemanticRerankAsync(
        IReadOnlyList<SkillResult> keywordResults,
        string userQuery,
        CancellationToken cancellationToken)
    {
        var argNotNull = _embeddingsService!;

        // 1. Embed user query
        var queryEmbedding = await argNotNull.EmbedAsync(userQuery, cancellationToken);

        // 2. Embed skill descriptions in batch
        var skillDescriptions = keywordResults
            .Select(r => r.Description ?? r.Name)
            .ToList();

        var skillEmbeddings = await argNotNull.EmbedBatchAsync(
            skillDescriptions, cancellationToken);

        // 3. Compute semantic scores (cosine similarity)
        var semanticScores = new Dictionary<string, float>();
        for (int i = 0; i < skillEmbeddings.Count; i++)
        {
            var similarity = argNotNull.CosineSimilarity(queryEmbedding, skillEmbeddings[i]);
            // Normalize cosine similarity from [-1, 1] to [0, 1]
            var normalizedScore = Math.Max(0, (similarity + 1) / 2);
            semanticScores[keywordResults[i].SkillId] = normalizedScore;
        }

        // 4. Apply RRF fusion: merge keyword rank + semantic rank → final score
        var rrfScores = ComputeRrfScores(keywordResults, semanticScores);

        // 5. Build result list with merged scores and confidence
        var results = keywordResults
            .Select((skill, idx) =>
            {
                var rrf = rrfScores[skill.SkillId];
                var semantic = semanticScores[skill.SkillId];
                var confidenceLevel = DetermineConfidenceLevel(rrf);

                return skill with
                {
                    SemanticScore = semantic,
                    FinalScore = rrf,
                    ConfidenceLevel = confidenceLevel,
                    ConfidenceSource = "semantic_reranked",
                    FinalRank = idx + 1
                };
            })
            .OrderByDescending(r => r.FinalScore)
            .ToList();

        // Update final ranks after re-ordering
        for (int i = 0; i < results.Count; i++)
        {
            results[i] = results[i] with { FinalRank = i + 1 };
        }

        _logger.LogDebug(
            "Semantic re-ranking complete: processed {Count} skills, " +
            "top result: {TopSkill} (final_score={Score:F3})",
            keywordResults.Count,
            results.FirstOrDefault()?.Name ?? "none",
            results.FirstOrDefault()?.FinalScore ?? 0);

        return results;
    }

    /// <summary>
    /// Computes RRF (Reciprocal Rank Fusion) scores combining keyword and semantic ranks.
    /// Formula: RRF_score = keyword_contribution + semantic_contribution
    /// where each contribution = 1 / (k + rank), k = 60 (standard RRF parameter).
    /// </summary>
    private Dictionary<string, float> ComputeRrfScores(
        IReadOnlyList<SkillResult> keywordResults,
        Dictionary<string, float> semanticScores)
    {
        var rrfScores = new Dictionary<string, float>();

        for (int i = 0; i < keywordResults.Count; i++)
        {
            var skill = keywordResults[i];
            var keywordRank = i + 1; // 1-based rank

            // Keyword RRF component: contribution of keyword rank
            var keywordRrf = 1f / (RrfK + keywordRank);

            // Semantic RRF component: infer rank from semantic score
            // Higher semantic score = lower (better) rank
            var semanticScore = semanticScores[skill.SkillId];
            var semanticRank = ComputeRankFromScore(semanticScore, keywordResults.Count);
            var semanticRrf = 1f / (RrfK + semanticRank);

            // Combined RRF score (no weighting; equal importance for Phase 2B)
            rrfScores[skill.SkillId] = keywordRrf + semanticRrf;
        }

        return rrfScores;
    }

    /// <summary>
    /// Infers rank (1-based) from semantic similarity score.
    /// Higher score → lower rank. Ensures consistent RRF comparison.
    /// </summary>
    private int ComputeRankFromScore(float score, int totalItems)
    {
        // Clamp score to [0, 1]
        var clamped = Math.Max(0, Math.Min(1, score));
        // Map score to rank: score 1.0 → rank 1, score 0.0 → rank totalItems
        var rank = (int)Math.Round((1 - clamped) * totalItems) + 1;
        return Math.Max(1, Math.Min(totalItems, rank));
    }

    /// <summary>
    /// Applies fallback confidence when semantic re-ranking is unavailable.
    /// Preserves original ranking and marks results as keyword-only.
    /// </summary>
    private List<SkillResult> ApplyFallbackConfidence(
        IReadOnlyList<SkillResult> keywordResults)
    {
        return keywordResults
            .Select((skill, idx) =>
                skill with
                {
                    FinalScore = skill.KeywordScore,
                    ConfidenceSource = "keyword_only",
                    FinalRank = idx + 1
                })
            .ToList();
    }

    /// <summary>
    /// Determines confidence level from merged score.
    /// </summary>
    private string DetermineConfidenceLevel(float score) =>
        score switch
        {
            >= 0.8f => "HIGH",
            >= 0.5f => "MEDIUM",
            _ => "LOW"
        };
}
