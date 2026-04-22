namespace OpenClawNet.Models.Abstractions;

public sealed record ChatRequest
{
    /// <summary>Model name. Null means use the provider's configured default.</summary>
    public string? Model { get; init; }
    public required IReadOnlyList<ChatMessage> Messages { get; init; }
    public IReadOnlyList<ToolDefinition>? Tools { get; init; }
    public double? Temperature { get; init; }
    public int? MaxTokens { get; init; }
}
