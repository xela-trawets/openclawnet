using System.Runtime.CompilerServices;
using Microsoft.Playwright;

namespace OpenClawNet.PlaywrightTests;

/// <summary>
/// Base class for Playwright tests that provides screenshot-on-failure infrastructure.
/// Automatically captures full-page screenshots when tests fail.
/// </summary>
public abstract class PlaywrightTestBase : IAsyncLifetime
{
    private readonly AppHostFixture _fixture;
    private IBrowserContext? _context;
    private IPage? _page;

    protected PlaywrightTestBase(AppHostFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// The Playwright page instance for this test. Available after InitializeAsync completes.
    /// </summary>
    protected IPage Page => _page ?? throw new InvalidOperationException("Page not initialized");

    /// <summary>
    /// The AppHost fixture for accessing base URLs and other test resources.
    /// </summary>
    protected AppHostFixture Fixture => _fixture;

    public virtual async Task InitializeAsync()
    {
        _context = await _fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true
        });
        _page = await _context.NewPageAsync();
        _page.SetDefaultTimeout(30_000);
    }

    public virtual async Task DisposeAsync()
    {
        if (_page is not null) await _page.CloseAsync();
        if (_context is not null) await _context.DisposeAsync();
    }

    /// <summary>
    /// Wraps a test action and captures a screenshot on failure.
    /// Screenshots are saved to TestResults/screenshots/ with format: {ClassName}_{MethodName}_{timestamp}.png
    /// </summary>
    /// <param name="testAction">The test code to execute</param>
    /// <param name="testMethodName">The name of the calling test method (auto-populated)</param>
    protected async Task WithScreenshotOnFailure(Func<Task> testAction, [CallerMemberName] string testMethodName = "")
    {
        try
        {
            await testAction();
        }
        catch (Exception)
        {
            await CaptureScreenshotAsync(testMethodName);
            throw;
        }
    }

    private async Task CaptureScreenshotAsync(string testMethodName)
    {
        if (_page is null) return;

        try
        {
            var className = GetType().Name;
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var screenshotDir = Path.Combine("TestResults", "screenshots");
            var filename = $"{className}_{testMethodName}_{timestamp}.png";
            var fullPath = Path.Combine(screenshotDir, filename);

            Directory.CreateDirectory(screenshotDir);

            await _page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = fullPath,
                FullPage = true
            });

            Console.WriteLine($"Screenshot saved: {fullPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to capture screenshot: {ex.Message}");
        }
    }
}
