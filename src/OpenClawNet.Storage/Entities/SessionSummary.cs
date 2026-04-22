namespace OpenClawNet.Storage.Entities;

public sealed class SessionSummary
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public int CoveredMessageCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public ChatSession Session { get; set; } = null!;
}
