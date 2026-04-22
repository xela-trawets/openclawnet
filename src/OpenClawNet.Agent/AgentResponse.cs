using OpenClawNet.Tools.Abstractions;

namespace OpenClawNet.Agent;

public sealed record AgentResponse
{
    public required string Content { get; init; }
    public IReadOnlyList<ToolResult> ToolResults { get; init; } = [];
    public int ToolCallCount { get; init; }
    public int TotalTokens { get; init; }
}

public sealed record AgentStreamEvent
{
    public AgentStreamEventType Type { get; init; }
    public string? Content { get; init; }
    public string? ToolName { get; init; }
    public string? ToolDescription { get; init; }
    public ToolResult? ToolResult { get; init; }
    public bool IsComplete { get; init; }

    /// <summary>
    /// Correlation id for a <see cref="AgentStreamEventType.ToolApprovalRequest"/> event.
    /// Echoed back by the client when POSTing the approval/denial decision.
    /// </summary>
    public Guid? RequestId { get; init; }

    /// <summary>
    /// JSON-serialized arguments the model wants to call the tool with.
    /// Surfaced in the approval card so the user can review what's about to run.
    /// </summary>
    public string? ToolArgsJson { get; init; }
}

public enum AgentStreamEventType
{
    ContentDelta,
    ToolApprovalRequest,
    ToolCallStart,
    ToolCallComplete,
    Complete,
    Error
}
