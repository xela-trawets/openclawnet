using System.Text.Json;

namespace OpenClawNet.Models.Abstractions;

public sealed record ToolDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public JsonDocument? Parameters { get; init; }
}

public sealed record ToolCall
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Arguments { get; init; }
}
