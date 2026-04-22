using OpenClawNet.Agent.ToolApproval;

namespace OpenClawNet.Gateway.Endpoints;

/// <summary>
/// HTTP endpoint that resolves a pending tool-approval request emitted on the
/// NDJSON chat stream as a <c>tool_approval</c> event.
///
/// Wave 4 PR-2 (Dallas). Contract documented in
/// <c>.squad/decisions/inbox/lambert-toolapproval-ui-pr1.md</c>:
///   POST /api/chat/tool-approval
///   { requestId: Guid, approved: bool, rememberForSession: bool }
///
/// Returns 200 when the decision was matched to a pending request, or 404 when
/// the request id is unknown (stale UI, server restart, request already resolved).
/// </summary>
public static class ToolApprovalEndpoints
{
    public static void MapToolApprovalEndpoints(this WebApplication app)
    {
        app.MapPost("/api/chat/tool-approval", (
            ToolApprovalDecisionRequest body,
            IToolApprovalCoordinator coordinator,
            ILogger<ToolApprovalDecisionRequest> logger) =>
        {
            if (body.RequestId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "requestId is required." });
            }

            var resolved = coordinator.TryResolve(
                body.RequestId,
                new ApprovalDecision(body.Approved, body.RememberForSession));

            if (!resolved)
            {
                logger.LogInformation(
                    "Tool approval decision for unknown or already-resolved request {RequestId}",
                    body.RequestId);
                return Results.NotFound(new { error = "Unknown or already-resolved approval request." });
            }

            return Results.Ok(new { resolved = true, requestId = body.RequestId });
        })
        .WithName("ResolveToolApproval")
        .WithTags("Chat")
        .WithDescription("Resolve a pending tool-approval request with the user's Approve/Deny decision.");
    }
}

/// <summary>
/// Body of <c>POST /api/chat/tool-approval</c>. Lambert's <c>ToolApprovalCard</c>
/// posts this when the user clicks Approve or Deny.
/// </summary>
public sealed record ToolApprovalDecisionRequest
{
    public Guid RequestId { get; init; }
    public bool Approved { get; init; }
    public bool RememberForSession { get; init; }
}
