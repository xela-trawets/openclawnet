namespace OpenClawNet.Services.Scheduler.Services;

/// <summary>
/// Configuration options for the JobScheduler service.
/// </summary>
public sealed class SchedulerOptions
{
    /// <summary>
    /// How often the scheduler polls for due jobs (in seconds).
    /// Default: 30 seconds.
    /// </summary>
    public int PollIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Whether the scheduler is enabled. If false, no jobs will be executed.
    /// Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum number of concurrent jobs that can run simultaneously.
    /// Default: 3.
    /// </summary>
    public int MaxConcurrentJobs { get; set; } = 3;

    /// <summary>
    /// Timeout for job execution (in seconds).
    /// Default: 300 seconds (5 minutes).
    /// </summary>
    public int JobTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Validates and clamps option values to safe ranges.
    /// </summary>
    public void Validate()
    {
        if (PollIntervalSeconds < 5) PollIntervalSeconds = 5;
        if (PollIntervalSeconds > 3600) PollIntervalSeconds = 3600;

        if (MaxConcurrentJobs < 1) MaxConcurrentJobs = 1;
        if (MaxConcurrentJobs > 20) MaxConcurrentJobs = 20;

        if (JobTimeoutSeconds < 10) JobTimeoutSeconds = 10;
        if (JobTimeoutSeconds > 7200) JobTimeoutSeconds = 7200;
    }
}
