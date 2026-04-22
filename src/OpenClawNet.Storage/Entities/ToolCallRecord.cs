namespace OpenClawNet.Storage.Entities;

public sealed class ToolCallRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid MessageId { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string? Result { get; set; }
    public bool Success { get; set; }
    public double DurationMs { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}
