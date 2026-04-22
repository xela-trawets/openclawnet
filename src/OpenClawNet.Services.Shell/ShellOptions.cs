namespace OpenClawNet.Services.Shell;

/// <summary>
/// Configuration options for the Shell service.
/// Bound from the <c>Services:Shell</c> configuration section.
/// </summary>
public sealed class ShellOptions
{
    /// <summary>Maximum execution time for a single shell command, in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Maximum captured stdout/stderr length (characters) per stream.</summary>
    public int MaxOutputLength { get; set; } = 10_000;
}
