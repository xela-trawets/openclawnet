namespace OpenClawNet.Agent.ToolApproval;

/// <summary>
/// User decision on a pending tool-approval request.
/// </summary>
/// <param name="Approved">True if the user approved the tool call, false if denied.</param>
/// <param name="RememberForSession">
/// When true (and Approved is true), subsequent calls to the same tool within the same
/// conversation will be auto-approved without prompting. Honored at session scope only —
/// not persisted across server restarts (per Bruno 2026-04-19 MVP defaults).
/// </param>
public sealed record ApprovalDecision(bool Approved, bool RememberForSession);
