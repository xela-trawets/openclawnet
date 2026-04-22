namespace OpenClawNet.Skills;

public sealed record SkillContent
{
    public required string Name { get; init; }
    public required string Content { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
}
