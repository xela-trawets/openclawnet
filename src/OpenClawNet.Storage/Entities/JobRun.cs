namespace OpenClawNet.Storage.Entities;

public sealed class JobRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public string Status { get; set; } = "running";
    public string? Result { get; set; }
    public string? Error { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    /// <summary>Snapshot of input parameters used for this run.</summary>
    public string? InputSnapshotJson { get; set; }

    /// <summary>Total tokens consumed by this run (prompt + completion).</summary>
    public int? TokensUsed { get; set; }

    /// <summary>Agent profile name used for execution (for auditing).</summary>
    public string? ExecutedByAgentProfile { get; set; }
    
    public ScheduledJob Job { get; set; } = null!;
}
