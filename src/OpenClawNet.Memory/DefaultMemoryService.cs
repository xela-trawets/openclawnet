using Microsoft.EntityFrameworkCore;
using OpenClawNet.Storage;
using OpenClawNet.Storage.Entities;

namespace OpenClawNet.Memory;

public sealed class DefaultMemoryService : IMemoryService
{
    private readonly IDbContextFactory<OpenClawDbContext> _contextFactory;
    
    public DefaultMemoryService(IDbContextFactory<OpenClawDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }
    
    public async Task<string?> GetSessionSummaryAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var summary = await db.Summaries
            .Where(s => s.SessionId == sessionId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        
        return summary?.Summary;
    }
    
    public async Task StoreSummaryAsync(Guid sessionId, string summary, int messageCount, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        db.Summaries.Add(new SessionSummary
        {
            SessionId = sessionId,
            Summary = summary,
            CoveredMessageCount = messageCount
        });
        await db.SaveChangesAsync(cancellationToken);
    }
    
    public async Task<IReadOnlyList<SummaryRecord>> GetAllSummariesAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Summaries
            .Where(s => s.SessionId == sessionId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SummaryRecord
            {
                Id = s.Id,
                SessionId = s.SessionId,
                Summary = s.Summary,
                CoveredMessageCount = s.CoveredMessageCount,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }
    
    public async Task<MemoryStats> GetStatsAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        
        var totalMessages = await db.Messages.CountAsync(m => m.SessionId == sessionId, cancellationToken);
        var summaries = await db.Summaries
            .Where(s => s.SessionId == sessionId)
            .ToListAsync(cancellationToken);
        
        return new MemoryStats
        {
            TotalMessages = totalMessages,
            SummaryCount = summaries.Count,
            CoveredMessages = summaries.Sum(s => s.CoveredMessageCount),
            LastSummaryAt = summaries.MaxBy(s => s.CreatedAt)?.CreatedAt
        };
    }
}
