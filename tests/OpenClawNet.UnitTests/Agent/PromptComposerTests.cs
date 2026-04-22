using FluentAssertions;
using Microsoft.Extensions.Options;
using OpenClawNet.Agent;
using OpenClawNet.Models.Abstractions;

namespace OpenClawNet.UnitTests.Agent;

public class PromptComposerTests
{
    private static readonly IWorkspaceLoader NoOpWorkspaceLoader = new FakeWorkspaceLoader(new BootstrapContext(null, null, null));
    private static readonly IOptions<WorkspaceOptions> DefaultWorkspaceOptions = Options.Create(new WorkspaceOptions());

    [Fact]
    public async Task ComposeAsync_IncludesSystemPrompt()
    {
        var composer = new DefaultPromptComposer(NoOpWorkspaceLoader, DefaultWorkspaceOptions);
        
        var context = new PromptContext
        {
            SessionId = Guid.NewGuid(),
            UserMessage = "Hello"
        };
        
        var messages = await composer.ComposeAsync(context);
        
        messages.Should().HaveCountGreaterThanOrEqualTo(2);
        messages[0].Role.Should().Be(ChatMessageRole.System);
        messages[0].Content.Should().Contain("OpenClaw");
        messages[^1].Role.Should().Be(ChatMessageRole.User);
        messages[^1].Content.Should().Be("Hello");
    }
    
    [Fact]
    public async Task ComposeAsync_IncludesSessionSummary()
    {
        var composer = new DefaultPromptComposer(NoOpWorkspaceLoader, DefaultWorkspaceOptions);
        
        var context = new PromptContext
        {
            SessionId = Guid.NewGuid(),
            UserMessage = "Continue",
            SessionSummary = "We discussed .NET architecture earlier."
        };
        
        var messages = await composer.ComposeAsync(context);
        
        messages[0].Content.Should().Contain("discussed .NET architecture");
    }
    
    [Fact]
    public async Task ComposeAsync_IncludesHistory()
    {
        var composer = new DefaultPromptComposer(NoOpWorkspaceLoader, DefaultWorkspaceOptions);
        
        var history = new List<ChatMessage>
        {
            new() { Role = ChatMessageRole.User, Content = "First message" },
            new() { Role = ChatMessageRole.Assistant, Content = "First reply" }
        };
        
        var context = new PromptContext
        {
            SessionId = Guid.NewGuid(),
            UserMessage = "Second message",
            History = history
        };
        
        var messages = await composer.ComposeAsync(context);
        
        // System + 2 history + 1 user = 4
        messages.Should().HaveCount(4);
    }

    // ── Workspace-aware fallback prompt ──────────────────────────────────────

    [Fact]
    public async Task ComposeAsync_FallbackPrompt_ContainsWorkspacePath()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), "test-workspace");
        var opts = Options.Create(new WorkspaceOptions { WorkspacePath = workspacePath });

        var composer = new DefaultPromptComposer(NoOpWorkspaceLoader, opts);
        var messages = await composer.ComposeAsync(new PromptContext
        {
            SessionId = Guid.NewGuid(),
            UserMessage = "ping"
        });

        messages[0].Content.Should().Contain(workspacePath,
            "the fallback system prompt should include the configured workspace path");
    }

    [Fact]
    public async Task ComposeAsync_FallbackPrompt_InstructsFileSystemToolUsage()
    {
        var composer = new DefaultPromptComposer(NoOpWorkspaceLoader, DefaultWorkspaceOptions);
        var messages = await composer.ComposeAsync(new PromptContext
        {
            SessionId = Guid.NewGuid(),
            UserMessage = "ping"
        });

        messages[0].Content.Should().Contain("file_system",
            "the fallback prompt should explicitly name the file_system tool");
        messages[0].Content.Should().NotContain("do not invoke any tools",
            "the old discouraging language should be removed");
    }

    [Fact]
    public async Task ComposeAsync_UsesAgentsMd_WhenWorkspaceProvides()
    {
        var agentsMdContent = "You are CUSTOM_AGENT, an expert in .NET.";
        var workspaceLoader = new FakeWorkspaceLoader(new BootstrapContext(agentsMdContent, null, null));

        var composer = new DefaultPromptComposer(workspaceLoader, DefaultWorkspaceOptions);
        var messages = await composer.ComposeAsync(new PromptContext
        {
            SessionId = Guid.NewGuid(),
            UserMessage = "Hi"
        });

        messages[0].Content.Should().Contain("CUSTOM_AGENT",
            "AGENTS.md persona should override the built-in fallback prompt");
    }

    private sealed class FakeWorkspaceLoader : IWorkspaceLoader
    {
        private readonly BootstrapContext _context;
        public FakeWorkspaceLoader(BootstrapContext context) => _context = context;
        public Task<BootstrapContext> LoadAsync(string workspacePath, CancellationToken ct = default)
            => Task.FromResult(_context);
    }
}
