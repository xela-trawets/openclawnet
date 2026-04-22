namespace OpenClawNet.Web.Models.Jobs;

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
    public string? AgentProfileName { get; init; }
}

public record JobDetailDto : JobDto
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
    public string? InputSnapshotJson { get; init; }
    public int? TokensUsed { get; init; }
    public string? ExecutedByAgentProfile { get; init; }
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
    public string? AgentProfileName { get; init; }
}

public sealed record JobStatsDto
{
    public int TotalRuns { get; init; }
    public int CompletedRuns { get; init; }
    public int FailedRuns { get; init; }
    public int RunningRuns { get; init; }
    public double SuccessRate { get; init; }
    public double AverageDurationSeconds { get; init; }
    public int TotalTokensUsed { get; init; }
}

public sealed record JobExecutionResultDto
{
    public Guid RunId { get; init; }
    public string Status { get; init; } = "";
    public string? Result { get; init; }
    public string? Error { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}

public sealed record JobTransitionResponse
{
    public Guid Id { get; init; }
    public string Status { get; init; } = "";
}

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

/// <summary>One row from a JobRun's persisted event timeline.</summary>
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

/// <summary>Built-in job template (read-only).</summary>
public sealed record JobTemplateDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public string? Category { get; init; }
    public string? DocsUrl { get; init; }
    public List<string> Prerequisites { get; init; } = [];
    public List<string> RequiredSecrets { get; init; } = [];
    public List<string> RequiredTools { get; init; } = [];
    public required CreateJobRequest DefaultJob { get; init; }
}
