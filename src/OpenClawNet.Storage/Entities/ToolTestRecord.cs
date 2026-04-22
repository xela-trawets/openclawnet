namespace OpenClawNet.Storage.Entities;

/// <summary>
/// Persisted result of the most recent tool test (Direct Invoke or Agent Probe).
/// Mirrors the LastTestedAt/Succeeded/Error pattern used by ModelProviderDefinition
/// and AgentProfileEntity.
/// </summary>
public class ToolTestRecord
{
    /// <summary>Tool name (matches <see cref="OpenClawNet.Tools.Abstractions.ITool.Name"/>).</summary>
    public string Name { get; set; } = null!;

    /// <summary>UTC timestamp of the last test attempt. Null = never tested.</summary>
    public DateTime? LastTestedAt { get; set; }

    /// <summary>Outcome of the last test. Null = never tested.</summary>
    public bool? LastTestSucceeded { get; set; }

    /// <summary>
    /// Error message or short success summary captured at the last test.
    /// Truncated to 1000 chars by callers.
    /// </summary>
    public string? LastTestError { get; set; }

    /// <summary>
    /// Mode used for the last test: <c>direct</c> (no LLM) or <c>probe</c>
    /// (LLM converts NL prompt → JSON args, then the tool is invoked).
    /// </summary>
    public string? LastTestMode { get; set; }
}
