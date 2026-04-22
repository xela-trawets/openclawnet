namespace OpenClawNet.Storage.Entities;

/// <summary>How a job is triggered.</summary>
public enum TriggerType
{
    /// <summary>Manually triggered by user action.</summary>
    Manual = 0,
    /// <summary>Scheduled via cron expression (recurring).</summary>
    Cron = 1,
    /// <summary>One-time scheduled execution.</summary>
    OneShot = 2,
    /// <summary>Triggered by external webhook/event.</summary>
    Webhook = 3
}
