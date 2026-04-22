using Microsoft.EntityFrameworkCore;
using OpenClawNet.Storage.Entities;

namespace OpenClawNet.Storage;

/// <summary>EF Core-backed <see cref="IToolTestRecordStore"/>.</summary>
public sealed class ToolTestRecordStore : IToolTestRecordStore
{
    private readonly IDbContextFactory<OpenClawDbContext> _dbFactory;

    public ToolTestRecordStore(IDbContextFactory<OpenClawDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<ToolTestRecord?> GetAsync(string toolName, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.ToolTestRecords.FindAsync([toolName], ct);
    }

    public async Task<IReadOnlyList<ToolTestRecord>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.ToolTestRecords.AsNoTracking().ToListAsync(ct);
    }

    public async Task SaveAsync(string toolName, bool succeeded, string? message, string mode, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var existing = await db.ToolTestRecords.FindAsync([toolName], ct);
        var truncated = message is { Length: > 1000 } ? message[..1000] : message;

        if (existing is null)
        {
            db.ToolTestRecords.Add(new ToolTestRecord
            {
                Name = toolName,
                LastTestedAt = DateTime.UtcNow,
                LastTestSucceeded = succeeded,
                LastTestError = truncated,
                LastTestMode = mode,
            });
        }
        else
        {
            existing.LastTestedAt = DateTime.UtcNow;
            existing.LastTestSucceeded = succeeded;
            existing.LastTestError = truncated;
            existing.LastTestMode = mode;
        }

        await db.SaveChangesAsync(ct);
    }
}
