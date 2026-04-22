namespace OpenClawNet.Skills;

public sealed record SkillDefinition
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Category { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public bool Enabled { get; init; } = true;
    public string? FilePath { get; init; }
    public IReadOnlyList<string> Examples { get; init; } = [];
    /// <summary>"installed" for marketplace installs, "built-in" for bundled skills.</summary>
    public string Source { get; init; } = "built-in";
}
