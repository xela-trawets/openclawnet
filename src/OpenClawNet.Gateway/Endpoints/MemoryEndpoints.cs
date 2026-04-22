using OpenClawNet.Memory;

namespace OpenClawNet.Gateway.Endpoints;

public static class MemoryEndpoints
{
    public static void MapMemoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/memory").WithTags("Memory");
        
        group.MapGet("/{sessionId:guid}/summary", async (Guid sessionId, IMemoryService memoryService) =>
        {
            var summary = await memoryService.GetSessionSummaryAsync(sessionId);
            return summary is not null
                ? Results.Ok(new { sessionId, summary })
                : Results.Ok(new { sessionId, summary = (string?)null, message = "No summary available" });
        })
        .WithName("GetSessionSummary");
        
        group.MapGet("/{sessionId:guid}/summaries", async (Guid sessionId, IMemoryService memoryService) =>
        {
            var summaries = await memoryService.GetAllSummariesAsync(sessionId);
            return Results.Ok(summaries);
        })
        .WithName("GetAllSummaries");
        
        group.MapGet("/{sessionId:guid}/stats", async (Guid sessionId, IMemoryService memoryService) =>
        {
            var stats = await memoryService.GetStatsAsync(sessionId);
            return Results.Ok(stats);
        })
        .WithName("GetMemoryStats");
    }
}
