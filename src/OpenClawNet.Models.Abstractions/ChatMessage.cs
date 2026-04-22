namespace OpenClawNet.Models.Abstractions;

public sealed record ChatMessage
{
    public required ChatMessageRole Role { get; init; }
    public required string Content { get; init; }
    public string? Name { get; init; }
    public string? ToolCallId { get; init; }
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }
}

public enum ChatMessageRole
{
    System,
    User,
    Assistant,
    Tool
}
