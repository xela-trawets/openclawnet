using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenClawNet.Storage;
using OpenClawNet.Storage.Entities;
using OpenClawNet.Tools.Abstractions;

namespace OpenClawNet.Tools.Scheduler;

public sealed class SchedulerTool : ITool
{
    private readonly IDbContextFactory<OpenClawDbContext> _contextFactory;
    private readonly ILogger<SchedulerTool> _logger;
    
    public SchedulerTool(IDbContextFactory<OpenClawDbContext> contextFactory, ILogger<SchedulerTool> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }
    
    public string Name => "schedule";
    public string Description => "Create, list, or cancel scheduled jobs and reminders.";
    
    public ToolMetadata Metadata => new()
    {
        Name = Name,
        Description = Description,
        ParameterSchema = JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "action": { "type": "string", "enum": ["create", "list", "cancel", "start", "pause", "resume"], "description": "Scheduler action" },
                "name": { "type": "string", "description": "Job name (for create)" },
                "prompt": { "type": "string", "description": "Prompt to execute when job runs (for create)" },
                "runAt": { "type": "string", "description": "ISO 8601 datetime for one-time jobs (for create)" },
                "cron": { "type": "string", "description": "Cron expression for recurring jobs (for create)" },
                "allowConcurrentRuns": { "type": "boolean", "description": "Allow overlapping runs of this job (default: false)" },
                "jobId": { "type": "string", "description": "Job ID (for cancel, start, pause, resume)" }
            },
            "required": ["action"]
        }
        """),
        RequiresApproval = false,
        Category = "scheduler",
        Tags = ["schedule", "reminder", "job", "cron", "automation"]
    };
    
    public async Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        
        try
        {
            var action = input.GetStringArgument("action");
            
            return action?.ToLowerInvariant() switch
            {
                "create" => await CreateJobAsync(input, sw, cancellationToken),
                "list" => await ListJobsAsync(sw, cancellationToken),
                "cancel" => await TransitionJobAsync(input, JobStatus.Cancelled, sw, cancellationToken),
                "start" => await TransitionJobAsync(input, JobStatus.Active, sw, cancellationToken),
                "pause" => await TransitionJobAsync(input, JobStatus.Paused, sw, cancellationToken),
                "resume" => await TransitionJobAsync(input, JobStatus.Active, sw, cancellationToken),
                _ => ToolResult.Fail(Name, "Unknown action. Use 'create', 'list', 'cancel', 'start', 'pause', or 'resume'.", sw.Elapsed)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduler tool error");
            return ToolResult.Fail(Name, ex.Message, sw.Elapsed);
        }
    }
    
    private async Task<ToolResult> CreateJobAsync(ToolInput input, Stopwatch sw, CancellationToken ct)
    {
        var name = input.GetStringArgument("name");
        var prompt = input.GetStringArgument("prompt");
        var runAtStr = input.GetStringArgument("runAt");
        var cron = input.GetStringArgument("cron");
        var allowConcurrentRuns = input.GetArgument<bool?>("allowConcurrentRuns") ?? false;
        
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(prompt))
        {
            return ToolResult.Fail(Name, "Both 'name' and 'prompt' are required for creating a job", sw.Elapsed);
        }
        
        DateTime? nextRun = null;
        bool isRecurring = !string.IsNullOrEmpty(cron);
        
        if (!string.IsNullOrEmpty(runAtStr) && DateTime.TryParse(runAtStr, out var parsedDate))
        {
            nextRun = parsedDate.ToUniversalTime();
        }
        else if (!isRecurring)
        {
            // Default to 1 hour from now
            nextRun = DateTime.UtcNow.AddHours(1);
        }
        
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        
        var job = new ScheduledJob
        {
            Name = name,
            Prompt = prompt,
            CronExpression = cron,
            NextRunAt = nextRun,
            IsRecurring = isRecurring,
            Status = JobStatus.Draft,
            AllowConcurrentRuns = allowConcurrentRuns
        };
        
        db.Jobs.Add(job);
        await db.SaveChangesAsync(ct);
        
        _logger.LogInformation("Created scheduled job: {Name} ({Id}), next run: {NextRun}, allowConcurrentRuns: {Concurrent}",
            name, job.Id, nextRun, allowConcurrentRuns);
        
        sw.Stop();
        return ToolResult.Ok(Name, $"Job created successfully:\n  ID: {job.Id}\n  Name: {name}\n  Next Run: {nextRun?.ToString("u") ?? "recurring"}\n  Recurring: {isRecurring}\n  Allow Concurrent Runs: {allowConcurrentRuns}", sw.Elapsed);
    }
    
    private async Task<ToolResult> ListJobsAsync(Stopwatch sw, CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        
        var jobs = await db.Jobs
            .OrderByDescending(j => j.CreatedAt)
            .Take(20)
            .ToListAsync(ct);
        
        if (jobs.Count == 0)
        {
            return ToolResult.Ok(Name, "No scheduled jobs found.", sw.Elapsed);
        }
        
        var sb = new StringBuilder();
        sb.AppendLine($"Found {jobs.Count} job(s):\n");
        
        foreach (var job in jobs)
        {
            sb.AppendLine($"  [{job.Status.ToString().ToLowerInvariant()}] {job.Name} (ID: {job.Id})");
            sb.AppendLine($"    Prompt: {job.Prompt[..Math.Min(80, job.Prompt.Length)]}...");
            if (job.NextRunAt.HasValue)
                sb.AppendLine($"    Next Run: {job.NextRunAt:u}");
            if (job.IsRecurring)
                sb.AppendLine($"    Cron: {job.CronExpression}");
            sb.AppendLine($"    Allow Concurrent Runs: {job.AllowConcurrentRuns}");
            sb.AppendLine();
        }
        
        sw.Stop();
        return ToolResult.Ok(Name, sb.ToString(), sw.Elapsed);
    }
    
    private async Task<ToolResult> TransitionJobAsync(ToolInput input, JobStatus targetStatus, Stopwatch sw, CancellationToken ct)
    {
        var jobIdStr = input.GetStringArgument("jobId");

        if (string.IsNullOrEmpty(jobIdStr) || !Guid.TryParse(jobIdStr, out var jobId))
        {
            return ToolResult.Fail(Name, "'jobId' is required and must be a valid GUID", sw.Elapsed);
        }

        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var job = await db.Jobs.FindAsync([jobId], ct);
        if (job is null)
        {
            return ToolResult.Fail(Name, $"Job not found: {jobId}", sw.Elapsed);
        }

        if (!JobStatusTransitions.IsAllowed(job.Status, targetStatus))
        {
            return ToolResult.Fail(Name,
                $"Cannot transition job from '{job.Status.ToString().ToLowerInvariant()}' to '{targetStatus.ToString().ToLowerInvariant()}'.",
                sw.Elapsed);
        }

        var previousStatus = job.Status;
        job.Status = targetStatus;

        _logger.LogInformation("Job '{Name}' ({Id}) transitioned {From} → {To}",
            job.Name, job.Id, previousStatus.ToString().ToLowerInvariant(), targetStatus.ToString().ToLowerInvariant());

        sw.Stop();
        await db.SaveChangesAsync(ct);
        return ToolResult.Ok(Name,
            $"Job '{job.Name}' ({job.Id}) is now {targetStatus.ToString().ToLowerInvariant()}.",
            sw.Elapsed);
    }
}
