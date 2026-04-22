namespace OpenClawNet.Tools.Web;

/// <summary>
/// Configuration options for <see cref="WebTool"/>.
/// Bound from the <c>Tools:Web</c> configuration section.
/// </summary>
public sealed class WebToolOptions
{
    /// <summary>HTTP request timeout, in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>Maximum response body length (characters) returned to the caller before truncation.</summary>
    public int MaxResponseLength { get; set; } = 50_000;
}
