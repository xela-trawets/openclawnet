using System.Net.Http.Json;
using Microsoft.Playwright;

namespace OpenClawNet.PlaywrightTests;

/// <summary>
/// Wave 4 PR-3 — E2E coverage for the tool-approval flow (un-skipped after PR-1 + PR-2 merged).
///
/// Tagged <c>Trait("Category", "ToolApproval")</c> so they don't run in default CI.
/// Run with: <c>dotnet test --filter "Category=ToolApproval"</c> against a live Aspire stack.
///
/// Reference: docs/analysis/tool-approval-design.md (Ripley, 2026-04-19).
/// Bruno's decisions baked in:
///   Q1 = per-tool-type, session-scoped approval cache
///   Q2 = schedule tool is EXEMPT (no approval prompt even on requiring profiles)
///   Q3 = cron jobs against requiring profiles fail-fast (see CronJob test in IntegrationTests)
///   Q4 = "Remember for this session" checkbox is offered
///   Q5 = browser tool stays in the requires-approval set
/// </summary>
[Collection("AppHost")]
[Trait("Category", "ToolApproval")]
public class ToolApprovalFlowTests : PlaywrightTestBase
{
    // Wave 4 PR-1 (Lambert UI) and PR-2 (Dallas backend) merged — tests live now.
    // Still gated by Trait("Category","ToolApproval") so they only run when explicitly selected.
    public ToolApprovalFlowTests(AppHostFixture fixture) : base(fixture) { }

    // ---------------------------------------------------------------------
    // Helpers — placeholder shapes so the file compiles cleanly today.
    // The post-merge implementer should replace these with real DTOs from
    // OpenClawNet.Models.Abstractions / Gateway client packages.
    // ---------------------------------------------------------------------

    private sealed record AgentProfileDraft(
        string Name,
        string Provider,
        string Model,
        string Instructions,
        bool RequireToolApproval);

    /// <summary>
    /// Creates (or upserts) an AgentProfile via the gateway's PUT /api/agent-profiles/{name}
    /// endpoint. The profile name is the natural key — it lives in the URL, not the body.
    /// </summary>
    private async Task<string> CreateProfileAsync(AgentProfileDraft draft)
    {
        using var http = Fixture.CreateGatewayHttpClient();
        var body = new
        {
            DisplayName = draft.Name,
            draft.Provider,
            draft.Model,
            draft.Instructions,
            EnabledTools = (string[]?)null,
            Temperature = (double?)null,
            MaxTokens = (int?)null,
            IsDefault = false,
            draft.RequireToolApproval
        };
        var response = await http.PutAsJsonAsync($"/api/agent-profiles/{Uri.EscapeDataString(draft.Name)}", body);
        response.EnsureSuccessStatusCode();
        return draft.Name;
    }

    /// <summary>
    /// TODO (post PR-1): tighten selector to the actual ToolApprovalCard
    /// component contract (e.g. <c>data-testid="tool-approval-card"</c>).
    /// </summary>
    private ILocator ApprovalCard() =>
        Page.Locator("[data-testid='tool-approval-card'], .tool-approval-card");

    private async Task SendChatMessageAsync(string text)
    {
        var input = Page.GetByTestId("chat-input");
        await input.FillAsync(text);
        var sendBtn = Page.GetByTestId("chat-send");
        await Microsoft.Playwright.Assertions.Expect(sendBtn).ToBeEnabledAsync(new() { Timeout = 5_000 });
        await sendBtn.ClickAsync();
    }

    // ---------------------------------------------------------------------
    // Scenario 1
    // ---------------------------------------------------------------------
    [SkippableFact]
    public async Task Profile_RequireApproval_True_PausesOnToolCall()
    {
        Skip.IfNot(Fixture.IsToolCapableModelAvailable, Fixture.ToolCapableModelSkipReason);
        await WithScreenshotOnFailure(async () =>
        {
            // Arrange: create a profile that requires approval and open chat against it.
            var profileName = await CreateProfileAsync(new AgentProfileDraft(
                Name: $"approval-required-{Guid.NewGuid():N}",
                Provider: "ollama", Model: AppHostFixture.ToolCapableTestModel,
                Instructions: "Use the browser tool to fetch https://example.com when asked.",
                RequireToolApproval: true));

            await Page.GotoAsync($"{Fixture.WebBaseUrl}/?profile={profileName}",
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // Act: send a prompt that should trigger a `browser` or `web_fetch` tool call.
            await SendChatMessageAsync("Please open example.com and tell me the title.");

            // Assert: ToolApprovalCard appears with one of the URL-fetching tools
            //          (the model may pick either; both are reasonable for this prompt)
            //          and the agent stream is paused (no tool_result yet).
            await ApprovalCard().WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });
            var cardText = await ApprovalCard().InnerTextAsync();
            Assert.True(
                cardText.Contains("browser", StringComparison.OrdinalIgnoreCase) ||
                cardText.Contains("web_fetch", StringComparison.OrdinalIgnoreCase),
                $"Expected approval card to reference 'browser' or 'web_fetch'. Card text: {cardText}");
            await AssertCardContainsAsync(ApprovalCard(), "example.com");
        });
    }

    private static async Task AssertCardContainsAsync(ILocator locator, string expected)
    {
        var text = await locator.InnerTextAsync();
        Assert.Contains(expected, text, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------------
    // Scenario 2
    // ---------------------------------------------------------------------
    [SkippableFact]
    public async Task Profile_RequireApproval_True_UserApproves_ContinuesExecution()
    {
        Skip.IfNot(Fixture.IsToolCapableModelAvailable, Fixture.ToolCapableModelSkipReason);
        await WithScreenshotOnFailure(async () =>
        {
            // Arrange: same setup as scenario 1.
            var profileName = await CreateProfileAsync(new AgentProfileDraft(
                $"approval-required-{Guid.NewGuid():N}", "ollama", AppHostFixture.ToolCapableTestModel,
                "Use the browser tool when asked.", RequireToolApproval: true));

            await Page.GotoAsync($"{Fixture.WebBaseUrl}/?profile={profileName}",
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await SendChatMessageAsync("Open example.com.");
            await ApprovalCard().WaitForAsync();

            // Act: click [Approve].
            await ApprovalCard().Locator("button:has-text('Approve')").ClickAsync();

            // Assert: the tool result streams in afterward and the assistant message completes.
            //   - approval card disappears
            //   - a tool_result event renders (look for tool result element)
            //   - the assistant turn reaches a "done" state (no spinner / final message rendered)
            await ApprovalCard().WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Hidden,
                Timeout = 30_000
            });
            await Page.Locator("[data-testid='tool-result']").First
                .WaitForAsync(new LocatorWaitForOptions { Timeout = 60_000 });
            await Page.Locator("[data-testid='assistant-message-complete']").First
                .WaitForAsync(new LocatorWaitForOptions { Timeout = 60_000 });
        });
    }

    // ---------------------------------------------------------------------
    // Scenario 3
    // ---------------------------------------------------------------------
    [SkippableFact]
    public async Task Profile_RequireApproval_True_UserDenies_StopsCleanly()
    {
        Skip.IfNot(Fixture.IsToolCapableModelAvailable, Fixture.ToolCapableModelSkipReason);
        await WithScreenshotOnFailure(async () =>
        {
            // Arrange.
            var profileName = await CreateProfileAsync(new AgentProfileDraft(
                $"approval-required-{Guid.NewGuid():N}", "ollama", AppHostFixture.ToolCapableTestModel,
                "Use the browser tool when asked.", RequireToolApproval: true));

            await Page.GotoAsync($"{Fixture.WebBaseUrl}/?profile={profileName}",
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await SendChatMessageAsync("Open example.com.");
            await ApprovalCard().WaitForAsync();

            // Act: click [Deny].
            await ApprovalCard().Locator("button:has-text('Deny')").ClickAsync();

            // Assert: agent emits a "tool denied" message (or equivalent) and the turn
            //         terminates cleanly with NO exception toast.
            await Page.Locator("text=/denied|cancelled|not approved/i").First
                .WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });
            Assert.Equal(0, await Page.Locator(".toast-error, [data-testid='error-toast']").CountAsync());
        });
    }

    // ---------------------------------------------------------------------
    // Scenario 4
    // ---------------------------------------------------------------------
    [SkippableFact]
    public async Task Profile_RequireApproval_False_AutoApproves()
    {
        Skip.IfNot(Fixture.IsToolCapableModelAvailable, Fixture.ToolCapableModelSkipReason);
        await WithScreenshotOnFailure(async () =>
        {
            // Arrange: profile that does NOT require approval.
            var profileName = await CreateProfileAsync(new AgentProfileDraft(
                $"auto-approve-{Guid.NewGuid():N}", "ollama", AppHostFixture.ToolCapableTestModel,
                "Use the browser tool when asked.", RequireToolApproval: false));

            await Page.GotoAsync($"{Fixture.WebBaseUrl}/?profile={profileName}",
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // Act: same prompt that would trigger a tool call.
            await SendChatMessageAsync("Open example.com.");

            // Assert: NO approval card renders; tool runs immediately and a result
            //         is observed within a short window.
            await Page.Locator("[data-testid='tool-result']").First
                .WaitForAsync(new LocatorWaitForOptions { Timeout = 60_000 });
            Assert.Equal(0, await ApprovalCard().CountAsync());
        });
    }

    // ---------------------------------------------------------------------
    // Scenario 5
    // ---------------------------------------------------------------------
    [SkippableFact]
    public async Task RememberForSession_SuppressesSubsequentPrompts()
    {
        Skip.IfNot(Fixture.IsToolCapableModelAvailable, Fixture.ToolCapableModelSkipReason);
        await WithScreenshotOnFailure(async () =>
        {
            // Arrange: profile=require, open chat.
            var profileName = await CreateProfileAsync(new AgentProfileDraft(
                $"approval-required-{Guid.NewGuid():N}", "ollama", AppHostFixture.ToolCapableTestModel,
                "Use the browser tool when asked.", RequireToolApproval: true));

            await Page.GotoAsync($"{Fixture.WebBaseUrl}/?profile={profileName}",
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // Act: first call prompts; user checks "Remember for this session" + Approve.
            await SendChatMessageAsync("Open example.com (first).");
            await ApprovalCard().WaitForAsync();
            await ApprovalCard().Locator("input[type='checkbox']").First.CheckAsync();
            await ApprovalCard().Locator("button:has-text('Approve')").ClickAsync();
            await ApprovalCard().WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Hidden
            });

            // Act 2: send a second message that triggers the SAME tool name in the
            //         SAME chat session.
            await SendChatMessageAsync("Now open example.org with the browser tool.");

            // Assert: no approval card appears; tool result arrives directly.
            await Page.Locator("[data-testid='tool-result']").Nth(1)
                .WaitForAsync(new LocatorWaitForOptions { Timeout = 60_000 });
            Assert.Equal(0, await ApprovalCard().CountAsync());
        });
    }

    // ---------------------------------------------------------------------
    // Scenario 6 — schedule tool is exempt (Bruno Q2 = No)
    // ---------------------------------------------------------------------
    [SkippableFact]
    public async Task ScheduleTool_Exempt_NoApprovalEvenWhenRequired()
    {
        Skip.IfNot(Fixture.IsToolCapableModelAvailable, Fixture.ToolCapableModelSkipReason);
        await WithScreenshotOnFailure(async () =>
        {
            // Arrange: requiring profile.
            var profileName = await CreateProfileAsync(new AgentProfileDraft(
                $"approval-required-{Guid.NewGuid():N}", "ollama", AppHostFixture.ToolCapableTestModel,
                "Use the schedule tool to create jobs when asked.", RequireToolApproval: true));

            await Page.GotoAsync($"{Fixture.WebBaseUrl}/?profile={profileName}",
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // Act: prompt that drives the agent to invoke the `schedule` tool.
            //       Wave 5 PR-C (Vasquez): make the cron syntax explicit so the model
            //       has minimal ambiguity about which tool to pick.
            await SendChatMessageAsync(
                "Use the schedule tool to create a cron job with expression '0 9 * * *' that prints 'hello'.");

            // Assert: NO approval card renders for `schedule` — it's exempt (Bruno Q2).
            //   A tool_complete event for `schedule` should land within 60s, surfacing
            //   the hidden [data-testid='tool-result'] sentinel (PR-B).
            await Page.Locator("[data-testid='tool-result']").First
                .WaitForAsync(new LocatorWaitForOptions { Timeout = 60_000 });
            Assert.Equal(0, await ApprovalCard().CountAsync());
        });
    }

    // ---------------------------------------------------------------------
    // Scenario 7 — browser + web_fetch always require approval on requiring profile
    // ---------------------------------------------------------------------
    [SkippableTheory]
    [InlineData("web_fetch", "Issue a single HTTP GET to https://example.com and tell me the status code.")]
    [InlineData("browser", "Drive a headless browser to https://example.com and read the page title.")]
    public async Task BrowserAndWebFetch_AlwaysRequireApproval_OnRequiringProfile(
        string expectedTool, string prompt)
    {
        Skip.IfNot(Fixture.IsToolCapableModelAvailable, Fixture.ToolCapableModelSkipReason);
        await WithScreenshotOnFailure(async () =>
        {
            // Arrange.
            var profileName = await CreateProfileAsync(new AgentProfileDraft(
                $"approval-required-{Guid.NewGuid():N}", "ollama", AppHostFixture.ToolCapableTestModel,
                $"Use the {expectedTool} tool when asked.", RequireToolApproval: true));

            await Page.GotoAsync($"{Fixture.WebBaseUrl}/?profile={profileName}",
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // Act.
            await SendChatMessageAsync(prompt);

            // Assert: approval card appears and references the expected tool name.
            await ApprovalCard().WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });
            await AssertCardContainsAsync(ApprovalCard(), expectedTool);
        });
    }
}
