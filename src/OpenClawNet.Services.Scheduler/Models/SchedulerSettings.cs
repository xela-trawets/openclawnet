namespace OpenClawNet.Services.Scheduler.Models;

/// <summary>
/// Runtime-configurable settings for the Scheduler service.
/// Editable via the Scheduler UI and persisted to scheduler-settings.json.
/// </summary>
public sealed class SchedulerSettings
{
    /// <summary>Maximum number of jobs that can execute concurrently.</summary>
    public int MaxConcurrentJobs { get; set; } = 3;

    /// <summary>Maximum seconds a single job execution may run before being cancelled.</summary>
    public int JobTimeoutSeconds { get; set; } = 300;

    /// <summary>How often the scheduler polls for due jobs, in seconds.</summary>
    public int PollIntervalSeconds { get; set; } = 30;
}
