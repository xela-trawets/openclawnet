using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OpenClawNet.Agent;
using OpenClawNet.Storage;

namespace OpenClawNet.UnitTests.Agent;

public sealed class AgentOrchestratorProfileTests
{
    [Fact]
    public async Task StreamAsync_PassesProfileInstructions_ToContext()
    {
        // Arrange: mock IAgentRuntime that captures the AgentContext
        AgentContext? capturedContext = null;
        var runtime = new Mock<IAgentRuntime>();
        runtime.Setup(r => r.ExecuteStreamAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .Callback<AgentContext, CancellationToken>((ctx, _) => capturedContext = ctx)
            .Returns(AsyncEnumerable.Empty<AgentStreamEvent>());

        var orchestrator = new AgentOrchestrator(
            runtime.Object,
            Mock.Of<IConversationStore>(),
            Mock.Of<IWorkspaceLoader>(),
            Options.Create(new WorkspaceOptions()),
            Mock.Of<ILogger<AgentOrchestrator>>());

        var request = new AgentRequest
        {
            SessionId = Guid.NewGuid(),
            UserMessage = "hello",
            AgentProfileInstructions = "You are a pirate.",
            AgentProfileName = "pirate"
        };

        // Act
        await foreach (var _ in orchestrator.StreamAsync(request)) { }

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext!.AgentProfileInstructions.Should().Be("You are a pirate.");
    }
}
