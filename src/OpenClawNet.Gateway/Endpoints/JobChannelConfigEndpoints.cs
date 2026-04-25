using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenClawNet.Storage;
using OpenClawNet.Storage.Entities;
using System.Text.Json;

namespace OpenClawNet.Gateway.Endpoints;

public static class JobChannelConfigEndpoints
{
    public static void MapJobChannelConfigEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/jobs/{jobId:guid}/channels").WithTags("Job Channel Configuration");

        // GET /api/jobs/{jobId}/channels — retrieve all channel configurations for a job
        group.MapGet("/", async (
            Guid jobId,
            [FromServices] IDbContextFactory<OpenClawDbContext> dbFactory,
            HttpContext httpContext) =>
        {
            if (!IsLoopbackRequest(httpContext))
                return Results.StatusCode(403);

            await using var db = await dbFactory.CreateDbContextAsync();

            var job = await db.Jobs.FindAsync(jobId);
            if (job is null)
                return Results.NotFound(new { error = "Job not found" });

            var configs = await db.JobChannelConfigurations
                .Where(c => c.JobId == jobId)
                .OrderBy(c => c.ChannelType)
                .ToListAsync();

            var dtos = configs.Select(c => new JobChannelConfigDto(
                c.Id,
                c.JobId,
                c.ChannelType,
                c.ChannelConfig,
                c.IsEnabled,
                c.CreatedAt,
                c.UpdatedAt
            )).ToList();

            return Results.Ok(dtos);
        })
        .WithName("GetJobChannelConfigurations");

        // GET /api/jobs/{jobId}/channels/{channelType} — retrieve single channel configuration
        group.MapGet("/{channelType}", async (
            Guid jobId,
            string channelType,
            [FromServices] IDbContextFactory<OpenClawDbContext> dbFactory,
            HttpContext httpContext) =>
        {
            if (!IsLoopbackRequest(httpContext))
                return Results.StatusCode(403);

            await using var db = await dbFactory.CreateDbContextAsync();

            var config = await db.JobChannelConfigurations
                .Where(c => c.JobId == jobId && c.ChannelType == channelType)
                .FirstOrDefaultAsync();

            if (config is null)
                return Results.NotFound(new { error = $"Channel configuration for '{channelType}' not found" });

            return Results.Ok(new JobChannelConfigDto(
                config.Id,
                config.JobId,
                config.ChannelType,
                config.ChannelConfig,
                config.IsEnabled,
                config.CreatedAt,
                config.UpdatedAt
            ));
        })
        .WithName("GetJobChannelConfiguration");

        // PUT /api/jobs/{jobId}/channels/{channelType} — create or update channel configuration
        group.MapPut("/{channelType}", async (
            Guid jobId,
            string channelType,
            [FromBody] UpdateJobChannelConfigRequest request,
            [FromServices] IDbContextFactory<OpenClawDbContext> dbFactory,
            HttpContext httpContext) =>
        {
            if (!IsLoopbackRequest(httpContext))
                return Results.StatusCode(403);

            await using var db = await dbFactory.CreateDbContextAsync();

            // Verify job exists
            var job = await db.Jobs.FindAsync(jobId);
            if (job is null)
                return Results.NotFound(new { error = "Job not found" });

            // Validate channel type (for MVP: hardcoded list)
            var supportedChannels = new[] { "GenericWebhook", "Teams", "Slack" };
            if (!supportedChannels.Contains(channelType, StringComparer.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new 
                { 
                    error = $"Unsupported channel type '{channelType}'", 
                    supported = supportedChannels 
                });
            }

            // Validate JSON config if provided
            if (!string.IsNullOrWhiteSpace(request.ChannelConfig))
            {
                try
                {
                    JsonSerializer.Deserialize<JsonElement>(request.ChannelConfig);
                }
                catch (JsonException ex)
                {
                    return Results.BadRequest(new { error = "Invalid JSON in ChannelConfig", detail = ex.Message });
                }
            }

            // Find or create configuration
            var config = await db.JobChannelConfigurations
                .Where(c => c.JobId == jobId && c.ChannelType == channelType)
                .FirstOrDefaultAsync();

            if (config is null)
            {
                config = new JobChannelConfiguration
                {
                    JobId = jobId,
                    ChannelType = channelType,
                    ChannelConfig = request.ChannelConfig,
                    IsEnabled = request.IsEnabled,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                db.JobChannelConfigurations.Add(config);
            }
            else
            {
                config.ChannelConfig = request.ChannelConfig;
                config.IsEnabled = request.IsEnabled;
                config.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();

            return Results.Ok(new JobChannelConfigDto(
                config.Id,
                config.JobId,
                config.ChannelType,
                config.ChannelConfig,
                config.IsEnabled,
                config.CreatedAt,
                config.UpdatedAt
            ));
        })
        .WithName("UpdateJobChannelConfiguration");

        // DELETE /api/jobs/{jobId}/channels/{channelType} — delete channel configuration
        group.MapDelete("/{channelType}", async (
            Guid jobId,
            string channelType,
            [FromServices] IDbContextFactory<OpenClawDbContext> dbFactory,
            HttpContext httpContext) =>
        {
            if (!IsLoopbackRequest(httpContext))
                return Results.StatusCode(403);

            await using var db = await dbFactory.CreateDbContextAsync();

            var config = await db.JobChannelConfigurations
                .Where(c => c.JobId == jobId && c.ChannelType == channelType)
                .FirstOrDefaultAsync();

            if (config is null)
                return Results.NotFound(new { error = $"Channel configuration for '{channelType}' not found" });

            db.JobChannelConfigurations.Remove(config);
            await db.SaveChangesAsync();

            return Results.NoContent();
        })
        .WithName("DeleteJobChannelConfiguration");
    }

    private static bool IsLoopbackRequest(HttpContext httpContext)
    {
        var remoteIp = httpContext.Connection.RemoteIpAddress;
        return remoteIp?.IsIPv4MappedToIPv6 == true 
            ? remoteIp.MapToIPv4().ToString() == "127.0.0.1"
            : remoteIp?.ToString() == "127.0.0.1" || remoteIp?.ToString() == "::1";
    }
}

// DTOs
public record JobChannelConfigDto(
    Guid Id,
    Guid JobId,
    string ChannelType,
    string? ChannelConfig,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record UpdateJobChannelConfigRequest(
    string? ChannelConfig,
    bool IsEnabled);
