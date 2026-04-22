using System.Text.Json;

namespace OpenClawNet.Tools.Abstractions;

public sealed record ToolMetadata
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required JsonDocument ParameterSchema { get; init; }
    public bool RequiresApproval { get; init; }
    public string Category { get; init; } = "general";
    public IReadOnlyList<string> Tags { get; init; } = [];
}
