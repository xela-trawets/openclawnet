using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenClawNet.Mcp.Abstractions;
using OpenClawNet.Storage.Entities;

namespace OpenClawNet.Storage;

/// <summary>
/// EF-backed <see cref="IMcpServerCatalog"/>. Maps the persisted entities to the
/// abstraction POCOs consumed by <c>OpenClawNet.Mcp.Core</c> so that assembly stays
/// free of an EF dependency.
/// </summary>
public sealed class McpServerCatalog : IMcpServerCatalog
{
    private readonly IDbContextFactory<OpenClawDbContext> _factory;

    public McpServerCatalog(IDbContextFactory<OpenClawDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<McpServerDefinition>> GetServersAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var rows = await db.McpServerDefinitions.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<McpToolOverride>> GetOverridesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var rows = await db.McpToolOverrides.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(r => new McpToolOverride
        {
            ServerId = r.ServerId,
            ToolName = r.ToolName,
            RequireApproval = r.RequireApproval,
            Disabled = r.Disabled,
        }).ToList();
    }

    private static McpServerDefinition Map(McpServerDefinitionEntity e)
    {
        var args = string.IsNullOrWhiteSpace(e.ArgsJson)
            ? Array.Empty<string>()
            : JsonSerializer.Deserialize<string[]>(e.ArgsJson) ?? Array.Empty<string>();

        return new McpServerDefinition
        {
            Id = e.Id,
            Name = e.Name,
            Transport = Enum.TryParse<McpTransport>(e.Transport, ignoreCase: true, out var t) ? t : McpTransport.InProcess,
            Command = e.Command,
            Args = args,
            EnvJson = e.EnvJson,
            Url = e.Url,
            HeadersJson = e.HeadersJson,
            Enabled = e.Enabled,
            IsBuiltIn = e.IsBuiltIn,
            LastError = e.LastError,
            LastSeenUtc = e.LastSeenUtc,
        };
    }
}
