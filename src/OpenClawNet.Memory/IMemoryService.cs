namespace OpenClawNet.Memory;

public interface IMemoryService
{
    Task<string?> GetSessionSummaryAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task StoreSummaryAsync(Guid sessionId, string summary, int messageCount, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SummaryRecord>> GetAllSummariesAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<MemoryStats> GetStatsAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

public sealed record SummaryRecord
{
    public Guid Id { get; init; }
    public Guid SessionId { get; init; }
    public required string Summary { get; init; }
    public int CoveredMessageCount { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record MemoryStats
{
    public int TotalMessages { get; init; }
    public int SummaryCount { get; init; }
    public int CoveredMessages { get; init; }
    public DateTime? LastSummaryAt { get; init; }
}
