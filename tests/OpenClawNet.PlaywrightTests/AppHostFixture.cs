using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace OpenClawNet.PlaywrightTests;

/// <summary>
/// Shared Aspire AppHost + Playwright fixture for all E2E tests.
/// Starts the full distributed application once, then reuses it across tests.
/// </summary>
public sealed class AppHostFixture : IAsyncLifetime
{
    /// <summary>
    /// Wave 5 PR-D (Vasquez): tool-capable Ollama model that the AppHost is forced
    /// to use for E2E. The previous default (gemma4:e2b) emits inconsistent function
    /// calls and made <c>ToolApprovalFlowTests</c> non-deterministic. qwen2.5:3b is
    /// small enough to run on a dev box but reliably tool-capable.
    /// </summary>
    public const string ToolCapableTestModel = "qwen2.5:3b";

    private DistributedApplication? _app;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public string WebBaseUrl { get; private set; } = string.Empty;
    public string GatewayBaseUrl { get; private set; } = string.Empty;
    public IBrowser Browser => _browser ?? throw new InvalidOperationException("Fixture not initialized");

    /// <summary>
    /// True when the configured tool-capable model (<see cref="ToolCapableTestModel"/>)
    /// is present in the local Ollama tag listing. Tool-approval E2E scenarios should
    /// <c>Skip.IfNot</c> on this when Ollama is unavailable or the model isn't pulled.
    /// </summary>
    public bool IsToolCapableModelAvailable { get; private set; }

    public string ToolCapableModelSkipReason { get; private set; } =
        $"Ollama model '{ToolCapableTestModel}' not available locally; pull it with `ollama pull {ToolCapableTestModel}`.";

    /// <summary>
    /// Creates an HttpClient configured for the gateway endpoint with SSL validation disabled.
    /// </summary>
    public HttpClient CreateGatewayHttpClient()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        return new HttpClient(handler) { BaseAddress = new Uri(GatewayBaseUrl) };
    }

    public async Task InitializeAsync()
    {
        // Wave 5 PR-D: pin a tool-capable model BEFORE the AppHost reads its config.
        // OPENCLAW_OLLAMA_MODEL is honoured first inside AppHost.cs:14-17.
        Environment.SetEnvironmentVariable("OPENCLAW_OLLAMA_MODEL", ToolCapableTestModel);

        // Best-effort probe of local Ollama; results expose IsToolCapableModelAvailable
        // so live tool-approval scenarios can Skip cleanly when the model isn't pulled.
        await ProbeOllamaModelAvailabilityAsync();

        // Build and start the Aspire AppHost
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.OpenClawNet_AppHost>();

        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        // Wait for both web and gateway resources to be running
        var webTask = _app.ResourceNotifications
            .WaitForResourceAsync("web", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(5));

        var gatewayTask = _app.ResourceNotifications
            .WaitForResourceAsync("gateway", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(5));

        await Task.WhenAll(webTask, gatewayTask);

        // Get endpoints
        WebBaseUrl = _app.GetEndpoint("web", "https").ToString().TrimEnd('/');
        GatewayBaseUrl = _app.GetEndpoint("gateway", "https").ToString().TrimEnd('/');

        // Initialize Playwright
        _playwright = await Playwright.CreateAsync();

        // Allow headed mode via environment variable: PLAYWRIGHT_HEADED=true
        var headed = Environment.GetEnvironmentVariable("PLAYWRIGHT_HEADED")
            ?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;

        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = !headed,
            SlowMo = headed ? 500 : 0  // Slow down for visibility when headed
        });
    }

    private async Task ProbeOllamaModelAvailabilityAsync()
    {
        var ollamaBase = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL")
            ?? "http://localhost:11434";
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            using var resp = await http.GetAsync($"{ollamaBase.TrimEnd('/')}/api/tags");
            if (!resp.IsSuccessStatusCode)
            {
                ToolCapableModelSkipReason =
                    $"Ollama at {ollamaBase} responded {(int)resp.StatusCode} to /api/tags; cannot verify '{ToolCapableTestModel}'.";
                IsToolCapableModelAvailable = false;
                return;
            }

            var body = await resp.Content.ReadAsStringAsync();
            // Cheap substring match — avoids pulling in an Ollama client just for this.
            IsToolCapableModelAvailable = body.Contains(ToolCapableTestModel, StringComparison.OrdinalIgnoreCase);
            if (!IsToolCapableModelAvailable)
            {
                ToolCapableModelSkipReason =
                    $"Ollama at {ollamaBase} is reachable but model '{ToolCapableTestModel}' is not pulled. Run: `ollama pull {ToolCapableTestModel}`.";
            }
        }
        catch (Exception ex)
        {
            IsToolCapableModelAvailable = false;
            ToolCapableModelSkipReason =
                $"Could not reach Ollama at {ollamaBase} ({ex.GetType().Name}: {ex.Message}); cannot verify '{ToolCapableTestModel}'.";
        }
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null) await _browser.DisposeAsync();
        _playwright?.Dispose();
        if (_app is not null) await _app.DisposeAsync();
    }
}
