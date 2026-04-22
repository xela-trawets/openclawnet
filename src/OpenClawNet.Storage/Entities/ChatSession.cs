namespace OpenClawNet.Storage.Entities;

public sealed class ChatSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "New Chat";
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string? AgentProfileName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public List<ChatMessageEntity> Messages { get; set; } = [];
    public List<SessionSummary> Summaries { get; set; } = [];
}
