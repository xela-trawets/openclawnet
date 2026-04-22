using Microsoft.EntityFrameworkCore;
using OpenClawNet.Storage;

namespace OpenClawNet.Gateway.Endpoints;

/// <summary>
/// Development-only endpoints for testing and debugging.
/// These are NOT mapped in production environments.
/// </summary>
public static class DevEndpoints
{
    public static void MapDevEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/dev").WithTags("Dev");

        group.MapPost("/reset", ResetAllAsync)
            .WithName("ResetAll")
            .WithDescription("⚠️ DEV ONLY: Wipes all data and resets to fresh state");
    }

    private static async Task<IResult> ResetAllAsync(
        IDbContextFactory<OpenClawDbContext> dbFactory,
        IModelProviderDefinitionStore providerStore,
        IAgentProfileStore profileStore)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        // Delete order respects foreign keys: children before parents, standalone last.
        var tables = new[]
        {
            "JobRuns",
            "Messages",
            "Summaries",
            "ToolCalls",
            "Jobs",
            "Sessions",
            "ProviderSettings",
            "AgentProfiles",
            "ModelProviders"
        };

        var deleted = new Dictionary<string, int>();

        foreach (var table in tables)
        {
            // Table names are hardcoded constants above — no user input, no injection risk.
            #pragma warning disable EF1002
            var rows = await db.Database.ExecuteSqlRawAsync($"DELETE FROM \"{table}\"");
            #pragma warning restore EF1002
            deleted[table] = rows;
        }

        // Re-seed defaults so the app is immediately usable
        await providerStore.SeedDefaultsAsync();
        await profileStore.GetDefaultAsync(); // seeds default agent profile

        return Results.Ok(new
        {
            message = "All data wiped and defaults re-seeded. App is in fresh state.",
            timestamp = DateTime.UtcNow,
            deleted
        });
    }
}
