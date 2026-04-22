using OpenClawNet.Storage.Entities;

namespace OpenClawNet.Storage;

/// <summary>
/// Persistence for the most recent tool test outcome (per tool name).
/// </summary>
public interface IToolTestRecordStore
{
    Task<ToolTestRecord?> GetAsync(string toolName, CancellationToken ct = default);

    Task<IReadOnlyList<ToolTestRecord>> ListAsync(CancellationToken ct = default);

    /// <summary>Upserts the test record for <paramref name="toolName"/>.</summary>
    Task SaveAsync(string toolName, bool succeeded, string? message, string mode, CancellationToken ct = default);
}
