namespace OpenClawNet.Tools.Abstractions;

public interface IToolApprovalPolicy
{
    Task<bool> RequiresApprovalAsync(string toolName, string arguments);
    Task<bool> IsApprovedAsync(string toolName, string arguments);
}

public sealed class AlwaysApprovePolicy : IToolApprovalPolicy
{
    public Task<bool> RequiresApprovalAsync(string toolName, string arguments) => Task.FromResult(false);
    public Task<bool> IsApprovedAsync(string toolName, string arguments) => Task.FromResult(true);
}
