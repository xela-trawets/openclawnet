namespace OpenClawNet.Agent.ToolApproval;

/// <summary>
/// Tools that bypass the approval gate even when the active <c>AgentProfile.RequireToolApproval</c>
/// is true. Bruno's MVP default (2026-04-19): the <c>schedule</c> tool is exempt because it
/// IS the scheduler — every job creation cannot itself require human approval.
/// </summary>
public static class ToolApprovalExemptions
{
    public static readonly IReadOnlySet<string> ExemptToolNames =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "schedule"
        };

    public static bool IsExempt(string toolName) => ExemptToolNames.Contains(toolName);
}
