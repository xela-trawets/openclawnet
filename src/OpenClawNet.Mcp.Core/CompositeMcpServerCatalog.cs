using OpenClawNet.Mcp.Abstractions;

namespace OpenClawNet.Mcp.Core;

/// <summary>
/// Wraps an underlying (typically EF-backed) <see cref="IMcpServerCatalog"/> and
/// merges in bundled in-process server definitions surfaced from
/// <see cref="BundledMcpServerRegistry"/>. The DB-backed catalog wins on Id collisions.
/// </summary>
public sealed class CompositeMcpServerCatalog : IMcpServerCatalog
{
    private readonly IMcpServerCatalog _inner;
    private readonly BundledMcpServerRegistry _bundled;

    public CompositeMcpServerCatalog(IMcpServerCatalog inner, BundledMcpServerRegistry bundled)
    {
        _inner = inner;
        _bundled = bundled;
    }

    public async Task<IReadOnlyList<McpServerDefinition>> GetServersAsync(CancellationToken cancellationToken = default)
    {
        var persisted = await _inner.GetServersAsync(cancellationToken).ConfigureAwait(false);
        var seenIds = new HashSet<Guid>(persisted.Select(p => p.Id));
        var combined = new List<McpServerDefinition>(persisted);
        foreach (var def in _bundled.Definitions)
        {
            if (seenIds.Add(def.Id))
                combined.Add(def);
        }
        return combined;
    }

    public Task<IReadOnlyList<McpToolOverride>> GetOverridesAsync(CancellationToken cancellationToken = default)
        => _inner.GetOverridesAsync(cancellationToken);
}
