using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenClawNet.Gateway.Services;
using OpenClawNet.Storage;
using OpenClawNet.Storage.Entities;

namespace OpenClawNet.Gateway.Endpoints;

public static class JobEndpoints
{
    public static void MapJobEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/jobs").WithTags("Jobs");

        group.MapGet("/", async (IDbContextFactory<OpenClawDbContext> dbFactory) =>
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var jobs = await db.Jobs
                .OrderByDescending(j => j.CreatedAt)
                .Take(50)
                .ToListAsync();

            var dtos = jobs.Select(j => ToDto(j)).ToList();
            return Results.Ok(dtos);
        })
        .WithName("ListJobs");

        group.MapPost("/", async (CreateJobRequest request, IDbContextFactory<OpenClawDbContext> dbFactory) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Prompt))
                return Results.BadRequest(new { error = "Name and Prompt are required." });

            await using var db = await dbFactory.CreateDbContextAsync();

            var isRecurring = !string.IsNullOrEmpty(request.CronExpression);

            DateTime? nextRun = request.RunAt?.ToUniversalTime();
            if (nextRun is null && !isRecurring)
                nextRun = DateTime.UtcNow.AddHours(1);

            var job = new ScheduledJob
            {
                Name = request.Name,
                Prompt = request.Prompt,
                CronExpression = request.CronExpression,
                NextRunAt = nextRun,
                IsRecurring = isRecurring,
                Status = JobStatus.Draft,
                StartAt = request.StartAt?.ToUniversalTime(),
                EndAt = request.EndAt?.ToUniversalTime(),
                TimeZone = request.TimeZone,
                NaturalLanguageSchedule = request.NaturalLanguageSchedule,
                AllowConcurrentRuns = request.AllowConcurrentRuns
            };

            db.Jobs.Add(job);
            await db.SaveChangesAsync();

            return Results.Created($"/api/jobs/{job.Id}", ToDto(job));
        })
        .WithName("CreateJob")
        .WithDescription("Create a new scheduled job");

        group.MapGet("/{jobId:guid}", async (Guid jobId, IDbContextFactory<OpenClawDbContext> dbFactory) =>
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var job = await db.Jobs
                .Include(j => j.Runs.OrderByDescending(r => r.StartedAt))
                .FirstOrDefaultAsync(j => j.Id == jobId);

            if (job is null) return Results.NotFound();

            return Results.Ok(new JobDetailDto
            {
                Id = job.Id,
                Name = job.Name,
                Prompt = job.Prompt,
                Status = job.Status.ToString().ToLowerInvariant(),
                IsRecurring = job.IsRecurring,
                CronExpression = job.CronExpression,
                NextRunAt = job.NextRunAt,
                LastRunAt = job.LastRunAt,
                CreatedAt = job.CreatedAt,
                StartAt = job.StartAt,
                EndAt = job.EndAt,
                TimeZone = job.TimeZone,
                NaturalLanguageSchedule = job.NaturalLanguageSchedule,
                AllowConcurrentRuns = job.AllowConcurrentRuns,
                Runs = job.Runs.Select(r => new JobRunDto
                {
                    Id = r.Id,
                    Status = r.Status,
                    Result = r.Result,
                    Error = r.Error,
                    StartedAt = r.StartedAt,
                    CompletedAt = r.CompletedAt
                }).ToList()
            });
        })
        .WithName("GetJob");

        group.MapPut("/{jobId:guid}", async (Guid jobId, CreateJobRequest request, IDbContextFactory<OpenClawDbContext> dbFactory) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Prompt))
                return Results.BadRequest(new { error = "Name and Prompt are required." });

            await using var db = await dbFactory.CreateDbContextAsync();
            var job = await db.Jobs.FindAsync(jobId);
            if (job is null) return Results.NotFound();

            if (!JobStatusTransitions.IsEditable(job.Status))
                return Results.Conflict(new { error = $"Job cannot be edited in '{job.Status.ToString().ToLowerInvariant()}' state. Only Draft or Paused jobs are editable." });

            job.Name = request.Name;
            job.Prompt = request.Prompt;
            job.CronExpression = request.CronExpression;
            job.IsRecurring = !string.IsNullOrEmpty(request.CronExpression);
            job.StartAt = request.StartAt?.ToUniversalTime();
            job.EndAt = request.EndAt?.ToUniversalTime();
            job.TimeZone = request.TimeZone;
            job.NaturalLanguageSchedule = request.NaturalLanguageSchedule;
            job.AllowConcurrentRuns = request.AllowConcurrentRuns;

            await db.SaveChangesAsync();

            return Results.Ok(ToDto(job));
        })
        .WithName("UpdateJob")
        .WithDescription("Update an existing scheduled job (only when Draft or Paused)");

        group.MapDelete("/{jobId:guid}", async (Guid jobId, IDbContextFactory<OpenClawDbContext> dbFactory) =>
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var job = await db.Jobs.FindAsync(jobId);
            if (job is null) return Results.NotFound();

            db.Jobs.Remove(job);
            await db.SaveChangesAsync();
            return Results.NoContent();
        })
        .WithName("DeleteJob");

        // ═══════════════════════════════════════════════════════════════
        // Job Execution Endpoints
        // ═══════════════════════════════════════════════════════════════

        group.MapPost("/{jobId:guid}/execute", async (
            Guid jobId,
            JobExecutionRequest? request,
            JobExecutor executor,
            CancellationToken ct) =>
        {
            var result = await executor.ExecuteJobAsync(
                jobId,
                request?.InputParameters,
                dryRun: false,
                ct);

            if (!result.IsSuccess)
            {
                return result.Error?.Contains("not found") == true
                    ? Results.NotFound(new { error = result.Error })
                    : Results.BadRequest(new { error = result.Error });
            }

            return Results.Ok(new JobExecutionResponse
            {
                Success = result.IsSuccess,
                RunId = result.RunId,
                Output = result.Output,
                TokensUsed = result.TokensUsed,
                DurationMs = (int)result.Duration.TotalMilliseconds,
                WasDryRun = result.WasDryRun
            });
        })
        .WithName("ExecuteJob")
        .WithDescription("Execute a job immediately with optional input parameter overrides");

        group.MapPost("/{jobId:guid}/dry-run", async (
            Guid jobId,
            JobExecutionRequest? request,
            JobExecutor executor,
            CancellationToken ct) =>
        {
            var result = await executor.ExecuteJobAsync(
                jobId,
                request?.InputParameters,
                dryRun: true,
                ct);

            if (!result.IsSuccess)
            {
                return result.Error?.Contains("not found") == true
                    ? Results.NotFound(new { error = result.Error })
                    : Results.BadRequest(new { error = result.Error });
            }

            return Results.Ok(new JobExecutionResponse
            {
                Success = result.IsSuccess,
                RunId = result.RunId,
                Output = result.Output,
                TokensUsed = result.TokensUsed,
                DurationMs = (int)result.Duration.TotalMilliseconds,
                WasDryRun = result.WasDryRun
            });
        })
        .WithName("DryRunJob")
        .WithDescription("Execute a job without persisting a JobRun (for testing prompts)");

        // ═══════════════════════════════════════════════════════════════
        // Job State Transition Endpoints
        // ═══════════════════════════════════════════════════════════════

        group.MapPost("/{jobId:guid}/start", async (Guid jobId, IDbContextFactory<OpenClawDbContext> dbFactory) =>
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var job = await db.Jobs.FindAsync(jobId);
            if (job is null) return Results.NotFound();

            if (!JobStatusTransitions.IsAllowed(job.Status, JobStatus.Active))
            {
                return Results.Conflict(new
                {
                    error = $"Cannot start job in '{job.Status.ToString().ToLowerInvariant()}' state. " +
                            $"Valid source states: Draft."
                });
            }

            job.Status = JobStatus.Active;
            await db.SaveChangesAsync();

            return Results.Ok(ToDto(job));
        })
        .WithName("StartJob")
        .WithDescription("Activate a Draft job (Draft → Active)");

        group.MapPost("/{jobId:guid}/pause", async (Guid jobId, IDbContextFactory<OpenClawDbContext> dbFactory) =>
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var job = await db.Jobs.FindAsync(jobId);
            if (job is null) return Results.NotFound();

            if (!JobStatusTransitions.IsAllowed(job.Status, JobStatus.Paused))
            {
                return Results.Conflict(new
                {
                    error = $"Cannot pause job in '{job.Status.ToString().ToLowerInvariant()}' state. " +
                            $"Valid source states: Active."
                });
            }

            job.Status = JobStatus.Paused;
            await db.SaveChangesAsync();

            return Results.Ok(ToDto(job));
        })
        .WithName("PauseJob")
        .WithDescription("Pause an Active job (Active → Paused)");

        group.MapPost("/{jobId:guid}/resume", async (Guid jobId, IDbContextFactory<OpenClawDbContext> dbFactory) =>
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var job = await db.Jobs.FindAsync(jobId);
            if (job is null) return Results.NotFound();

            if (!JobStatusTransitions.IsAllowed(job.Status, JobStatus.Active))
            {
                return Results.Conflict(new
                {
                    error = $"Cannot resume job in '{job.Status.ToString().ToLowerInvariant()}' state. " +
                            $"Valid source states: Paused."
                });
            }

            job.Status = JobStatus.Active;
            await db.SaveChangesAsync();

            return Results.Ok(ToDto(job));
        })
        .WithName("ResumeJob")
        .WithDescription("Resume a Paused job (Paused → Active)");

        // ═══════════════════════════════════════════════════════════════
        // Job Stats and Runs Endpoints
        // ═══════════════════════════════════════════════════════════════

        group.MapGet("/{jobId:guid}/stats", async (Guid jobId, IDbContextFactory<OpenClawDbContext> dbFactory) =>
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var job = await db.Jobs.FindAsync(jobId);
            if (job is null) return Results.NotFound();

            var runs = await db.JobRuns
                .Where(r => r.JobId == jobId)
                .ToListAsync();

            var totalRuns = runs.Count;
            var completedRuns = runs.Count(r => r.Status == "completed");
            var failedRuns = runs.Count(r => r.Status == "failed");
            var totalTokens = runs.Where(r => r.TokensUsed.HasValue).Sum(r => r.TokensUsed!.Value);
            var avgTokens = completedRuns > 0
                ? runs.Where(r => r.Status == "completed" && r.TokensUsed.HasValue)
                       .Average(r => r.TokensUsed!.Value)
                : 0;

            var avgDurationMs = completedRuns > 0
                ? runs.Where(r => r.Status == "completed" && r.CompletedAt.HasValue)
                       .Average(r => (r.CompletedAt!.Value - r.StartedAt).TotalMilliseconds)
                : 0;

            var successRate = totalRuns > 0 ? (double)completedRuns / totalRuns : 0.0;
            var lastRunAt = runs.OrderByDescending(r => r.StartedAt).FirstOrDefault()?.StartedAt;

            return Results.Ok(new JobStatsResponse
            {
                JobId = jobId,
                TotalRuns = totalRuns,
                CompletedRuns = completedRuns,
                FailedRuns = failedRuns,
                SuccessRate = successRate,
                TotalTokensUsed = totalTokens,
                AverageTokensPerRun = (int)avgTokens,
                AverageDurationMs = (int)avgDurationMs,
                LastRunAt = lastRunAt
            });
        })
        .WithName("GetJobStats")
        .WithDescription("Get aggregated statistics for a job");

        group.MapGet("/{jobId:guid}/runs", async (
            Guid jobId,
            int? limit,
            IDbContextFactory<OpenClawDbContext> dbFactory) =>
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var job = await db.Jobs.FindAsync(jobId);
            if (job is null) return Results.NotFound();

            var pageSize = Math.Min(limit ?? 50, 100);
            var runs = await db.JobRuns
                .Where(r => r.JobId == jobId)
                .OrderByDescending(r => r.StartedAt)
                .Take(pageSize)
                .Select(r => new JobRunDto
                {
                    Id = r.Id,
                    Status = r.Status,
                    Result = r.Result,
                    Error = r.Error,
                    StartedAt = r.StartedAt,
                    CompletedAt = r.CompletedAt,
                    TokensUsed = r.TokensUsed,
                    InputSnapshot = r.InputSnapshotJson
                })
                .ToListAsync();

            return Results.Ok(runs);
        })
        .WithName("GetJobRuns")
        .WithDescription("Get paginated list of job runs (default 50, max 100)");

        // Per-run event timeline (persisted, survives restart). See JobRunEvent.
        group.MapGet("/{jobId:guid}/runs/{runId:guid}/events", async (
            Guid jobId,
            Guid runId,
            IDbContextFactory<OpenClawDbContext> dbFactory) =>
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var run = await db.JobRuns.FirstOrDefaultAsync(r => r.Id == runId && r.JobId == jobId);
            if (run is null) return Results.NotFound();

            var events = await db.JobRunEvents
                .Where(e => e.JobRunId == runId)
                .OrderBy(e => e.Sequence)
                .Select(e => new JobRunEventDto
                {
                    Id = e.Id,
                    Sequence = e.Sequence,
                    Timestamp = e.Timestamp,
                    Kind = e.Kind,
                    ToolName = e.ToolName,
                    ArgumentsJson = e.ArgumentsJson,
                    ResultJson = e.ResultJson,
                    Message = e.Message,
                    DurationMs = e.DurationMs,
                    TokensUsed = e.TokensUsed
                })
                .ToListAsync();

            return Results.Ok(events);
        })
        .WithName("GetJobRunEvents")
        .WithDescription("Get the persisted event timeline for a single job run.");

        // Built-in job templates (read-only, embedded at build time).
        group.MapGet("/templates", ([Microsoft.AspNetCore.Mvc.FromServices] OpenClawNet.Gateway.Services.JobTemplates.JobTemplatesProvider provider) =>
            Results.Ok(provider.GetAll()))
            .WithName("ListJobTemplates")
            .WithDescription("List all built-in job templates that can be used to seed a new job.");

        group.MapGet("/templates/{id}", (string id, [Microsoft.AspNetCore.Mvc.FromServices] OpenClawNet.Gateway.Services.JobTemplates.JobTemplatesProvider provider) =>
        {
            var t = provider.Get(id);
            return t is null ? Results.NotFound() : Results.Ok(t);
        })
        .WithName("GetJobTemplate")
        .WithDescription("Get a single built-in job template by id.");
    }

    private static JobDto ToDto(ScheduledJob j) => new()
    {
        Id = j.Id,
        Name = j.Name,
        Prompt = j.Prompt,
        Status = j.Status.ToString().ToLowerInvariant(),
        IsRecurring = j.IsRecurring,
        CronExpression = j.CronExpression,
        NextRunAt = j.NextRunAt,
        LastRunAt = j.LastRunAt,
        CreatedAt = j.CreatedAt,
        StartAt = j.StartAt,
        EndAt = j.EndAt,
        TimeZone = j.TimeZone,
        NaturalLanguageSchedule = j.NaturalLanguageSchedule,
        AllowConcurrentRuns = j.AllowConcurrentRuns
    };
}

public sealed record CreateJobRequest
{
    public required string Name { get; init; }
    public required string Prompt { get; init; }
    public string? CronExpression { get; init; }
    public DateTime? RunAt { get; init; }
    public DateTime? StartAt { get; init; }
    public DateTime? EndAt { get; init; }
    public string? TimeZone { get; init; }
    public string? NaturalLanguageSchedule { get; init; }
    public bool AllowConcurrentRuns { get; init; }
}

public record JobDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Prompt { get; init; }
    public required string Status { get; init; }
    public bool IsRecurring { get; init; }
    public string? CronExpression { get; init; }
    public DateTime? NextRunAt { get; init; }
    public DateTime? LastRunAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? StartAt { get; init; }
    public DateTime? EndAt { get; init; }
    public string? TimeZone { get; init; }
    public string? NaturalLanguageSchedule { get; init; }
    public bool AllowConcurrentRuns { get; init; }
}

public sealed record JobDetailDto : JobDto
{
    public List<JobRunDto> Runs { get; init; } = [];
}

public sealed record JobRunDto
{
    public Guid Id { get; init; }
    public required string Status { get; init; }
    public string? Result { get; init; }
    public string? Error { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int? TokensUsed { get; init; }
    public string? InputSnapshot { get; init; }
}

/// <summary>One row from the persisted event timeline for a single JobRun.</summary>
public sealed record JobRunEventDto
{
    public Guid Id { get; init; }
    public int Sequence { get; init; }
    public DateTime Timestamp { get; init; }
    public required string Kind { get; init; }
    public string? ToolName { get; init; }
    public string? ArgumentsJson { get; init; }
    public string? ResultJson { get; init; }
    public string? Message { get; init; }
    public int? DurationMs { get; init; }
    public int? TokensUsed { get; init; }
}

// ═══════════════════════════════════════════════════════════════
// Execution DTOs
// ═══════════════════════════════════════════════════════════════

public sealed record JobExecutionRequest
{
    public Dictionary<string, object>? InputParameters { get; init; }
}

public sealed record JobExecutionResponse
{
    public bool Success { get; init; }
    public Guid? RunId { get; init; }
    public string? Output { get; init; }
    public int TokensUsed { get; init; }
    public int DurationMs { get; init; }
    public bool WasDryRun { get; init; }
}

public sealed record JobStatsResponse
{
    public Guid JobId { get; init; }
    public int TotalRuns { get; init; }
    public int CompletedRuns { get; init; }
    public int FailedRuns { get; init; }
    public double SuccessRate { get; init; }
    public int TotalTokensUsed { get; init; }
    public int AverageTokensPerRun { get; init; }
    public int AverageDurationMs { get; init; }
    public DateTime? LastRunAt { get; init; }
}
