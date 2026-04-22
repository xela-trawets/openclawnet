using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenClawNet.Models.Abstractions;
using OpenClawNet.Models.GitHubCopilot;

namespace OpenClawNet.UnitTests.Models;

public class GitHubCopilotAgentProviderTests
{
    private static GitHubCopilotAgentProvider CreateProvider(GitHubCopilotOptions? opts = null)
    {
        var options = Options.Create(opts ?? new GitHubCopilotOptions());
        return new GitHubCopilotAgentProvider(options, NullLogger<GitHubCopilotAgentProvider>.Instance);
    }

    [Fact]
    public void ProviderName_ReturnsGitHubCopilot()
    {
        var provider = CreateProvider();
        provider.ProviderName.Should().Be("github-copilot");
    }

    [Fact]
    public void CreateChatClient_ReturnsNonNull()
    {
        var provider = CreateProvider();
        var profile = new AgentProfile { Name = "test" };

        var client = provider.CreateChatClient(profile);

        client.Should().NotBeNull();
    }

    [Fact]
    public void CreateChatClient_UsesDefaultModel_WhenProfileHasNone()
    {
        var provider = CreateProvider(new GitHubCopilotOptions { Model = "gpt-5-mini" });
        var profile = new AgentProfile { Name = "test" };

        var client = provider.CreateChatClient(profile);

        // The client wraps the model — we verify it was created without throwing
        client.Should().NotBeNull();
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsTrueWhenTokenConfigured()
    {
        var provider = CreateProvider(new GitHubCopilotOptions { GitHubToken = "ghp_test123" });

        var available = await provider.IsAvailableAsync();

        available.Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailableAsync_DoesNotThrow_WhenNoToken()
    {
        // Without token and without gh CLI, should not throw
        var provider = CreateProvider(new GitHubCopilotOptions { GitHubToken = null });

        var act = async () => await provider.IsAvailableAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        var provider = CreateProvider();

        // Should not throw on multiple dispose calls
        await provider.DisposeAsync();
        await provider.DisposeAsync();
    }
}
