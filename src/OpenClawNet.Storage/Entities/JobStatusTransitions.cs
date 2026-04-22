namespace OpenClawNet.Storage.Entities;

public static class JobStatusTransitions
{
    private static readonly HashSet<(JobStatus From, JobStatus To)> _allowed = new()
    {
        (JobStatus.Draft, JobStatus.Active),
        (JobStatus.Draft, JobStatus.Cancelled),
        (JobStatus.Active, JobStatus.Paused),
        (JobStatus.Active, JobStatus.Cancelled),
        (JobStatus.Active, JobStatus.Completed),
        (JobStatus.Paused, JobStatus.Active),
        (JobStatus.Paused, JobStatus.Cancelled),
    };

    public static bool IsAllowed(JobStatus from, JobStatus to) =>
        _allowed.Contains((from, to));

    public static bool IsTerminal(JobStatus status) =>
        status is JobStatus.Completed or JobStatus.Cancelled;

    public static bool IsEditable(JobStatus status) =>
        status is JobStatus.Draft or JobStatus.Paused;
}
