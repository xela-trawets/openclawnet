using Microsoft.Extensions.AI;
using OpenClawNet.Gateway.Services.Mcp;
using OpenClawNet.Mcp.Abstractions;
using OpenClawNet.Tools.Abstractions;

namespace OpenClawNet.Gateway.Endpoints;

/// <summary>
/// Minimal-API surface for the /settings/mcp Blazor pages.
/// </summary>
public static class McpServerEndpoints
{
    public static void MapMcpServerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/mcp").WithTags("MCP Servers");

        // ── Servers (CRUD + test) ───────────────────────────────────────────

        group.MapGet("/servers", async (McpServerCatalogService svc, CancellationToken ct) =>
        {
            var items = await svc.ListAsync(ct);
            return Results.Ok(items);
        }).WithName("ListMcpServers");

        group.MapGet("/servers/{id:guid}", async (Guid id, McpServerCatalogService svc, CancellationToken ct) =>
        {
            var item = await svc.GetAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).WithName("GetMcpServer");

        group.MapPost("/servers", async (McpServerWriteRequest req, McpServerCatalogService svc, CancellationToken ct) =>
        {
            var (created, error) = await svc.CreateAsync(req, ct);
            if (error is not null) return Results.BadRequest(new { error });
            return Results.Created($"/api/mcp/servers/{created!.Id}", created);
        }).WithName("CreateMcpServer");

        group.MapPut("/servers/{id:guid}", async (Guid id, McpServerWriteRequest req, McpServerCatalogService svc, CancellationToken ct) =>
        {
            var result = await svc.UpdateAsync(id, req, ct);
            if (result.NotFound) return Results.NotFound();
            if (result.Forbidden) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (result.Error is not null) return Results.BadRequest(new { error = result.Error });
            return Results.Ok(result.Item);
        }).WithName("UpdateMcpServer");

        group.MapDelete("/servers/{id:guid}", async (Guid id, McpServerCatalogService svc, CancellationToken ct) =>
        {
            var result = await svc.DeleteAsync(id, ct);
            if (result.NotFound) return Results.NotFound();
            if (result.Forbidden) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Results.NoContent();
        }).WithName("DeleteMcpServer");

        group.MapPost("/servers/{id:guid}/test", async (Guid id, McpServerCatalogService svc, CancellationToken ct) =>
        {
            var result = await svc.TestAsync(id, ct);
            return Results.Ok(result);
        }).WithName("TestMcpServer");

        group.MapPost("/servers/test-config", async (McpServerWriteRequest req, McpServerCatalogService svc, CancellationToken ct) =>
        {
            var result = await svc.TestConfigAsync(req, ct);
            return Results.Ok(result);
        }).WithName("TestMcpServerConfig");

        // ── Registry proxy ──────────────────────────────────────────────────

        group.MapGet("/registry/search", async (
            string? q,
            string? cursor,
            int? limit,
            IMcpRegistryClient registry,
            CancellationToken ct) =>
        {
            try
            {
                var result = await registry.SearchAsync(q, cursor, limit ?? 20, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.Json(new
                {
                    error = "Registry unreachable.",
                    detail = ex.Message,
                    entries = Array.Empty<McpRegistryEntry>(),
                }, statusCode: StatusCodes.Status502BadGateway);
            }
        }).WithName("SearchMcpRegistry");

        // ── Curated suggestions ─────────────────────────────────────────────

        group.MapGet("/suggestions", (McpSuggestionsProvider provider) =>
        {
            return Results.Ok(provider.GetAll());
        }).WithName("ListMcpSuggestions");

        group.MapPost("/suggestions/{id}/install", async (
            string id,
            McpSuggestionsProvider provider,
            McpServerCatalogService svc,
            CancellationToken ct) =>
        {
            var suggestion = provider.GetById(id);
            if (suggestion is null) return Results.NotFound(new { error = $"Unknown suggestion '{id}'." });

            var req = new McpServerWriteRequest
            {
                Name = suggestion.Name,
                Transport = suggestion.Transport,
                Command = suggestion.Command,
                Args = suggestion.Args.ToList(),
                Url = suggestion.Url,
                Env = suggestion.RequiresEnv.Count > 0
                    ? suggestion.RequiresEnv.ToDictionary(k => k, _ => string.Empty)
                    : null,
                Enabled = false,
            };

            var (created, error) = await svc.CreateAsync(req, ct);
            if (error is not null) return Results.BadRequest(new { error });
            return Results.Created($"/api/mcp/servers/{created!.Id}", created);
        }).WithName("InstallMcpSuggestion");

        // ── Tools picker (PR-D) ─────────────────────────────────────────────
        // Powers the AgentProfiles "Enabled tools" hierarchical multi-select. Returns
        // every visible tool grouped by its owning server. Storage-form names are what
        // the agent profile persists; descriptions feed the picker tooltips.

        group.MapGet("/tools/available", async (
            McpServerCatalogService catalog,
            IMcpToolProvider toolProvider,
            IToolRegistry toolRegistry,
            CancellationToken ct) =>
        {
            var servers = await catalog.ListAsync(ct);
            var groups = new List<McpToolGroupDto>();

            // 1) MCP servers (bundled + user-defined). Empty list when the server is disabled
            //    or not running — front-end still surfaces the row so users can see why.
            var mcpToolNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in servers)
            {
                IReadOnlyList<AITool> tools = Array.Empty<AITool>();
                if (s.Enabled && s.IsRunning)
                {
                    try { tools = await toolProvider.GetToolsForServerAsync(s.Id.ToString(), ct); }
                    catch { tools = Array.Empty<AITool>(); }
                }

                var dtoTools = tools
                    .Select(t =>
                    {
                        var storage = t is IMcpAITool m ? m.StorageName : $"{Slug(s.Name)}.{(t as AIFunction)?.Name ?? string.Empty}";
                        mcpToolNames.Add((t as AIFunction)?.Name ?? string.Empty);
                        return new McpToolEntryDto(storage, (t as AIFunction)?.Description ?? string.Empty);
                    })
                    .OrderBy(t => t.Name, StringComparer.Ordinal)
                    .ToList();

                groups.Add(new McpToolGroupDto(s.Id.ToString(), s.Name, IsLegacy: false, dtoTools));
            }

            // 2) Virtual "scheduler" group — anything still flowing through the legacy
            //    ITool path that wasn't shadowed by an MCP tool of the same name.
            var legacyTools = toolRegistry.GetAllTools()
                .Where(t => !mcpToolNames.Contains(t.Name))
                .Select(t => new McpToolEntryDto($"scheduler.{t.Name}", t.Description ?? string.Empty))
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .ToList();

            if (legacyTools.Count > 0)
                groups.Add(new McpToolGroupDto("scheduler", "scheduler", IsLegacy: true, legacyTools));

            return Results.Ok(groups);
        }).WithName("ListAvailableMcpTools");
    }

    private static string Slug(string name)
    {
        var buf = new char[name.Length];
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            buf[i] = char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_';
        }
        return new string(buf);
    }
}

public sealed record McpToolGroupDto(string ServerId, string ServerName, bool IsLegacy, IReadOnlyList<McpToolEntryDto> Tools);
public sealed record McpToolEntryDto(string Name, string Description);
