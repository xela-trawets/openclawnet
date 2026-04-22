namespace OpenClawNet.Models.Abstractions;

public sealed record ChatResponse
{
    public required string Content { get; init; }
    public required ChatMessageRole Role { get; init; }
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }
    public required string Model { get; init; }
    public UsageInfo? Usage { get; init; }
    public string? FinishReason { get; init; }
}

public sealed record ChatResponseChunk
{
    public string? Content { get; init; }
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }
    public string? FinishReason { get; init; }
}

public sealed record UsageInfo
{
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public int TotalTokens { get; init; }
}
