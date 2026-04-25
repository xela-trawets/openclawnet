namespace OpenClawNet.Storage.Entities;

/// <summary>
/// Stores per-job channel delivery configuration.
/// Each row represents one delivery channel for a specific job.
/// </summary>
public sealed class JobChannelConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>Reference to the scheduled job this configuration belongs to.</summary>
    public Guid JobId { get; set; }
    
    /// <summary>Channel type: "GenericWebhook", "Teams", "Slack".</summary>
    public string ChannelType { get; set; } = string.Empty;
    
    /// <summary>JSON configuration for this channel (e.g., {"webhookUrl":"https://..."}).</summary>
    public string? ChannelConfig { get; set; }
    
    /// <summary>Whether this channel is enabled for delivery.</summary>
    public bool IsEnabled { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>Navigation property to the parent job.</summary>
    public ScheduledJob? Job { get; set; }
}
