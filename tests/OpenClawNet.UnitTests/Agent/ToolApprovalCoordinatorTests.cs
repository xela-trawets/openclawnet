using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenClawNet.Agent.ToolApproval;

namespace OpenClawNet.UnitTests.Agent;

/// <summary>
/// Wave 4 PR-2 (Dallas): coverage for the in-memory tool-approval coordinator.
///
/// Bruno's MVP defaults baked in:
///   • No timeout — wait indefinitely until the user clicks (or the stream is cancelled).
///   • "Remember for this session" suppresses subsequent prompts in the same conversation.
///   • Cancelled stream does NOT leak a stranded TaskCompletionSource.
/// </summary>
public sealed class ToolApprovalCoordinatorTests
{
    private static ToolApprovalCoordinator NewCoordinator()
        => new(NullLogger<ToolApprovalCoordinator>.Instance);

    [Fact]
    public async Task RequestApproval_ReturnsApprovedDecision_WhenResolvedWithApprove()
    {
        var coord = NewCoordinator();
        var requestId = Guid.NewGuid();

        var awaiting = coord.RequestApprovalAsync(requestId, CancellationToken.None);
        var resolved = coord.TryResolve(requestId, new ApprovalDecision(Approved: true, RememberForSession: false));

        resolved.Should().BeTrue();
        var decision = await awaiting;
        decision.Approved.Should().BeTrue();
        decision.RememberForSession.Should().BeFalse();
    }

    [Fact]
    public async Task RequestApproval_ReturnsDeniedDecision_WhenResolvedWithDeny()
    {
        var coord = NewCoordinator();
        var requestId = Guid.NewGuid();

        var awaiting = coord.RequestApprovalAsync(requestId, CancellationToken.None);
        coord.TryResolve(requestId, new ApprovalDecision(Approved: false, RememberForSession: false));

        var decision = await awaiting;
        decision.Approved.Should().BeFalse();
    }

    [Fact]
    public void TryResolve_ReturnsFalse_WhenRequestUnknown()
    {
        var coord = NewCoordinator();
        coord.TryResolve(Guid.NewGuid(), new ApprovalDecision(true, false)).Should().BeFalse();
    }

    [Fact]
    public async Task RequestApproval_ThrowsCanceled_WhenCancellationTokenFires()
    {
        var coord = NewCoordinator();
        using var cts = new CancellationTokenSource();
        var requestId = Guid.NewGuid();

        var awaiting = coord.RequestApprovalAsync(requestId, cts.Token);
        cts.Cancel();

        var act = async () => await awaiting;
        await act.Should().ThrowAsync<OperationCanceledException>();

        // Late resolve attempts should be no-ops because the entry was removed on cancellation.
        coord.TryResolve(requestId, new ApprovalDecision(true, false)).Should().BeFalse();
    }

    [Fact]
    public void RememberApproval_TracksToolNamePerSession_CaseInsensitive()
    {
        var coord = NewCoordinator();
        var sessionA = Guid.NewGuid();
        var sessionB = Guid.NewGuid();

        coord.RememberApproval(sessionA, "browser");
        coord.IsToolApprovedForSession(sessionA, "browser").Should().BeTrue();
        coord.IsToolApprovedForSession(sessionA, "BROWSER").Should().BeTrue();
        coord.IsToolApprovedForSession(sessionA, "shell").Should().BeFalse();
        coord.IsToolApprovedForSession(sessionB, "browser").Should().BeFalse();
    }

    [Fact]
    public void ForgetSession_ClearsAllRememberedToolsForThatSession()
    {
        var coord = NewCoordinator();
        var sessionId = Guid.NewGuid();

        coord.RememberApproval(sessionId, "browser");
        coord.RememberApproval(sessionId, "shell");
        coord.ForgetSession(sessionId);

        coord.IsToolApprovedForSession(sessionId, "browser").Should().BeFalse();
        coord.IsToolApprovedForSession(sessionId, "shell").Should().BeFalse();
    }

    [Fact]
    public void Exemptions_Schedule_IsExemptFromApproval()
    {
        ToolApprovalExemptions.IsExempt("schedule").Should().BeTrue();
        ToolApprovalExemptions.IsExempt("SCHEDULE").Should().BeTrue();
        ToolApprovalExemptions.IsExempt("shell").Should().BeFalse();
        ToolApprovalExemptions.IsExempt("browser").Should().BeFalse();
        ToolApprovalExemptions.IsExempt("web_fetch").Should().BeFalse();
    }
}
