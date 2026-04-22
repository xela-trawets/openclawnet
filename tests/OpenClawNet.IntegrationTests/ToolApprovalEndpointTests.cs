using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OpenClawNet.Agent.ToolApproval;

namespace OpenClawNet.IntegrationTests;

/// <summary>
/// Wave 4 PR-2 (Dallas) — covers the gateway HTTP contract for tool approval.
///
/// Documented in <c>.squad/decisions/inbox/lambert-toolapproval-ui-pr1.md</c>:
///   POST /api/chat/tool-approval  { requestId, approved, rememberForSession }
///
/// We exercise the endpoint without Playwright by registering a pending request
/// directly on the singleton coordinator, then POSTing the decision and asserting
/// the awaiting Task resolves to the same decision.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ToolApprovalEndpointTests(GatewayWebAppFactory factory)
    : IClassFixture<GatewayWebAppFactory>
{
    [Fact]
    public async Task PostApproval_Approved_ResolvesPendingRequest()
    {
        var client = factory.CreateClient();
        var coordinator = factory.Services.GetRequiredService<IToolApprovalCoordinator>();

        var requestId = Guid.NewGuid();
        var awaiting = coordinator.RequestApprovalAsync(requestId, CancellationToken.None);

        var response = await client.PostAsJsonAsync("/api/chat/tool-approval", new
        {
            requestId,
            approved = true,
            rememberForSession = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var decision = await awaiting.WaitAsync(TimeSpan.FromSeconds(5));
        decision.Approved.Should().BeTrue();
        decision.RememberForSession.Should().BeTrue();
    }

    [Fact]
    public async Task PostApproval_Denied_ResolvesPendingRequestAsDenied()
    {
        var client = factory.CreateClient();
        var coordinator = factory.Services.GetRequiredService<IToolApprovalCoordinator>();

        var requestId = Guid.NewGuid();
        var awaiting = coordinator.RequestApprovalAsync(requestId, CancellationToken.None);

        var response = await client.PostAsJsonAsync("/api/chat/tool-approval", new
        {
            requestId,
            approved = false,
            rememberForSession = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var decision = await awaiting.WaitAsync(TimeSpan.FromSeconds(5));
        decision.Approved.Should().BeFalse();
    }

    [Fact]
    public async Task PostApproval_UnknownRequestId_Returns404()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chat/tool-approval", new
        {
            requestId = Guid.NewGuid(),
            approved = true,
            rememberForSession = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostApproval_EmptyRequestId_Returns400()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chat/tool-approval", new
        {
            requestId = Guid.Empty,
            approved = true,
            rememberForSession = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
