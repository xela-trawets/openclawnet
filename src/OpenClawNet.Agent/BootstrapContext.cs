namespace OpenClawNet.Agent;

/// <summary>
/// Holds the content loaded from workspace bootstrap files.
/// Null fields indicate the corresponding file was not found in the workspace directory.
/// </summary>
/// <param name="AgentsMd">Content of AGENTS.md — agent persona and behavioral instructions.</param>
/// <param name="SoulMd">Content of SOUL.md — core values and principles.</param>
/// <param name="UserMd">Content of USER.md — user profile and preferences.</param>
public sealed record BootstrapContext(
    string? AgentsMd,
    string? SoulMd,
    string? UserMd);
