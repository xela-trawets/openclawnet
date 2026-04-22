namespace OpenClawNet.Agent;

/// <summary>
/// Loads workspace bootstrap files (AGENTS.md, SOUL.md, USER.md) from a directory.
/// These files allow operators and users to customize agent persona, values, and preferences
/// without modifying application code.
/// </summary>
public interface IWorkspaceLoader
{
    /// <summary>
    /// Loads all bootstrap files from the specified workspace directory.
    /// Missing files result in null fields on the returned <see cref="BootstrapContext"/>.
    /// </summary>
    /// <param name="workspacePath">Absolute path to the workspace directory.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="BootstrapContext"/> containing the content of each file that was found.</returns>
    Task<BootstrapContext> LoadAsync(string workspacePath, CancellationToken ct = default);
}
