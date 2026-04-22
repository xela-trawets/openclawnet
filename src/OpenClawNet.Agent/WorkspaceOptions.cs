namespace OpenClawNet.Agent;

/// <summary>
/// Configuration options for the workspace loader.
/// Bind from <c>Agent</c> configuration section or set programmatically.
/// </summary>
public sealed class WorkspaceOptions
{
    /// <summary>
    /// Absolute path to the workspace directory containing bootstrap files
    /// (AGENTS.md, SOUL.md, USER.md). Defaults to the application base directory.
    /// Override via <c>Agent:WorkspacePath</c> in configuration.
    /// </summary>
    public string WorkspacePath { get; set; } = AppContext.BaseDirectory;
}
