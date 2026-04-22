namespace OpenClawNet.Services.Browser;

/// <summary>
/// Configuration options for the Browser service (Playwright-backed).
/// Bound from the <c>Services:Browser</c> configuration section.
/// </summary>
public sealed class BrowserOptions
{
    /// <summary>Page navigation timeout, in milliseconds.</summary>
    public int NavigationTimeoutMs { get; set; } = 30_000;

    /// <summary>Maximum extracted text length (characters) returned by extract-text before truncation.</summary>
    public int MaxExtractedTextLength { get; set; } = 5_000;
}
