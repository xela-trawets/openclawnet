namespace OpenClawNet.Models.GitHubCopilot;

/// <summary>
/// Configuration options for the GitHub Copilot SDK provider.
/// Authentication resolves in order: <see cref="GitHubToken"/> → environment
/// variables (<c>COPILOT_GITHUB_TOKEN</c>, <c>GH_TOKEN</c>, <c>GITHUB_TOKEN</c>)
/// → logged-in <c>gh</c> CLI user.
/// </summary>
public sealed class GitHubCopilotOptions
{
    /// <summary>
    /// GitHub personal-access or OAuth token. When set, takes priority over
    /// environment variables and CLI-based auth.
    /// </summary>
    public string? GitHubToken { get; set; }

    /// <summary>
    /// Model to use for completions (e.g. "gpt-5-mini", "gpt-5", "claude-sonnet-4.5").
    /// </summary>
    public string Model { get; set; } = "gpt-5-mini";

    /// <summary>
    /// Optional path to a custom Copilot CLI executable.
    /// When <c>null</c> the SDK uses its bundled CLI.
    /// </summary>
    public string? CliPath { get; set; }
}
