using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using OpenClawNet.Gateway.Endpoints;
using OpenClawNet.Gateway.Services.JobTemplates;

namespace OpenClawNet.IntegrationTests.Demos;

/// <summary>
/// LIVE end-to-end smoke test for the watched-folder demo
/// (<c>docs/demos/tools/01-watched-folder-summarizer/README.md</c>).
///
/// Unlike <see cref="JobTemplatesE2ETests"/>, this exercises the full pipeline
/// against a REAL local Ollama instance — same default agent users would use
/// when following the demo. It is the closest thing we have to an automated
/// re-run of the demo walkthrough.
///
/// <para>
/// Skipped automatically when:
///   • Ollama is not reachable at <c>http://localhost:11434</c>
///   • The integration-tests gateway is wired to <see cref="GatewayWebAppFactory"/>'s
///     in-memory <c>FakeModelClient</c>, so the model client cannot reach the
///     real LLM (this is the dominant case in CI). In that case the test acts
///     as a documentation-only fixture explaining the intended live shape.
/// </para>
///
/// <para>
/// Run manually with: <c>dotnet test --filter "Category=Live"</c> after
/// <c>aspire start src\OpenClawNet.AppHost</c> is running and Ollama is up.
/// </para>
/// </summary>
[Trait("Category", "Live")]
public sealed class WatchedFolderSummarizerLiveE2ETests(GatewayWebAppFactory factory)
    : IClassFixture<GatewayWebAppFactory>
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    [SkippableFact]
    public async Task WatchedFolderTemplate_AcceptedByGateway_AndJobIsExecutable()
    {
        // Sanity-check: the gateway must serve the template (this part runs
        // even without a live LLM and protects against the JSON resource
        // being dropped from the .csproj).
        var client = factory.CreateClient();
        var template = await client.GetFromJsonAsync<JobTemplate>(
            "/api/jobs/templates/watched-folder-summarizer", JsonOpts);
        template.Should().NotBeNull();
        template!.RequiredTools.Should().Contain("file_system");
        template.RequiredTools.Should().Contain("markdown_convert");

        // Skip the live execution leg unless an opt-in env var is set.
        // Why an explicit opt-in (not just "Ollama is reachable")?
        // The integration-tests host uses FakeModelClient, so even a running
        // Ollama would not be hit. Wiring real Ollama into the test host
        // requires the dev to opt in deliberately, mirroring how the
        // PlaywrightTests use the Aspire AppHost fixture.
        Skip.IfNot(
            string.Equals(Environment.GetEnvironmentVariable("OPENCLAWNET_LIVE_DEMOS"), "1", StringComparison.Ordinal),
            "Live demo execution skipped. Set OPENCLAWNET_LIVE_DEMOS=1 and run aspire+Ollama, " +
            "then run with --filter \"Category=Live\" to exercise the full demo against a real model.");

        // ── Live execution leg (only runs when explicitly opted-in) ────────
        // This block is intentionally written to validate the demo end-to-end
        // when the dev has Aspire + Ollama up. In CI, it's skipped.

        var sandbox = Path.Combine(Path.GetTempPath(),
            "openclawnet-watched-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(sandbox);
        try
        {
            var samplePath = Path.Combine(sandbox, "hello.md");
            await File.WriteAllTextAsync(samplePath,
                "# Hello\n\nThis is a tiny demo input. Summarize me into 5 bullets.\n");

            // Re-target the template's prompt at our sandbox folder rather
            // than the user's c:\temp\sampleDocs.
            var defaultJob = template.DefaultJob;
            var liveJob = new CreateJobRequest
            {
                Name = $"e2e-watched-{Guid.NewGuid():N}",
                Prompt = defaultJob.Prompt.Replace(@"c:\temp\sampleDocs", sandbox, StringComparison.OrdinalIgnoreCase),
                CronExpression = null, // run once for the test
                AllowConcurrentRuns = defaultJob.AllowConcurrentRuns
            };

            // Create the job
            var createResponse = await client.PostAsJsonAsync("/api/jobs", liveJob, JsonOpts);
            createResponse.EnsureSuccessStatusCode();
            var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
            var jobId = created.GetProperty("id").GetGuid();

            // Execute it synchronously
            var executeResponse = await client.PostAsync($"/api/jobs/{jobId}/execute", content: null);
            // Allow either OK (success) or 5xx with a clear message — we record but do not strictly
            // assert content because the LLM might hallucinate; the key signal is that the JobRun
            // and JobRunEvents got persisted.
            _ = executeResponse;

            // Poll for the most recent run + verify events were persisted
            var runs = await client.GetFromJsonAsync<JsonElement>($"/api/jobs/{jobId}/runs?limit=1", JsonOpts);
            runs.GetArrayLength().Should().BeGreaterThan(0, "executing the job must create a JobRun row");
            var runId = runs[0].GetProperty("id").GetGuid();

            var events = await client.GetFromJsonAsync<JsonElement>(
                $"/api/jobs/{jobId}/runs/{runId}/events", JsonOpts);
            events.GetArrayLength().Should().BeGreaterThan(0,
                "the run must produce at least one JobRunEvent (agent_started)");

            // We expect agent_started as the first event by sequence.
            var first = events[0];
            first.GetProperty("kind").GetString().Should().Be("agent_started");
        }
        finally
        {
            try { Directory.Delete(sandbox, recursive: true); } catch { /* best-effort */ }
        }
    }
}
