using Microsoft.Extensions.Logging;

namespace OpenClawNet.Agent;

/// <summary>
/// Reads AGENTS.md, SOUL.md, and USER.md from a workspace directory and returns their
/// contents as a <see cref="BootstrapContext"/>. Missing files are silently skipped.
/// </summary>
public sealed class WorkspaceLoader : IWorkspaceLoader
{
    private readonly ILogger<WorkspaceLoader> _logger;

    private static readonly string[] BootstrapFileNames =
    [
        "AGENTS.md",
        "SOUL.md",
        "USER.md"
    ];

    public WorkspaceLoader(ILogger<WorkspaceLoader> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<BootstrapContext> LoadAsync(string workspacePath, CancellationToken ct = default)
    {
        string? agentsMd = null;
        string? soulMd   = null;
        string? userMd   = null;

        foreach (var fileName in BootstrapFileNames)
        {
            var path = Path.Combine(workspacePath, fileName);

            if (!File.Exists(path))
            {
                _logger.LogDebug("Workspace bootstrap file not found, skipping: {Path}", path);
                continue;
            }

            try
            {
                var content = await File.ReadAllTextAsync(path, ct);
                _logger.LogDebug("Loaded workspace bootstrap file: {Path} ({Bytes} bytes)", path, content.Length);

                switch (fileName)
                {
                    case "AGENTS.md": agentsMd = content; break;
                    case "SOUL.md":   soulMd   = content; break;
                    case "USER.md":   userMd   = content; break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read workspace bootstrap file: {Path}", path);
            }
        }

        return new BootstrapContext(agentsMd, soulMd, userMd);
    }
}
