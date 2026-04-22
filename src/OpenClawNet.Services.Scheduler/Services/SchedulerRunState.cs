namespace OpenClawNet.Services.Scheduler.Services;

/// <summary>
/// Thread-safe singleton tracking the number of currently running jobs.
/// Shared between <see cref="SchedulerPollingService"/> and HTTP endpoints.
/// </summary>
public sealed class SchedulerRunState
{
    private int _runningCount;

    public int RunningJobCount => _runningCount;

    public void Increment() => Interlocked.Increment(ref _runningCount);
    public void Decrement() => Interlocked.Decrement(ref _runningCount);
}
