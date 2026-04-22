using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace OpenClawNet.Agent.ToolApproval;

/// <summary>
/// In-memory implementation of <see cref="IToolApprovalCoordinator"/>.
/// Singleton — must be thread-safe because requests originate on agent runtime threads
/// and resolutions arrive on HTTP request threads.
/// </summary>
public sealed class ToolApprovalCoordinator : IToolApprovalCoordinator
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ApprovalDecision>> _pending = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> _rememberedBySession = new();
    private readonly ILogger<ToolApprovalCoordinator> _logger;

    public ToolApprovalCoordinator(ILogger<ToolApprovalCoordinator> logger)
    {
        _logger = logger;
    }

    public Task<ApprovalDecision> RequestApprovalAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<ApprovalDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(requestId, tcs))
        {
            throw new InvalidOperationException($"Approval request {requestId} is already pending.");
        }

        // Wire cancellation so a dropped client / cancelled stream doesn't leak the TCS forever.
        var registration = cancellationToken.Register(() =>
        {
            if (_pending.TryRemove(requestId, out var stale))
            {
                stale.TrySetCanceled(cancellationToken);
            }
        });

        _logger.LogDebug("Approval request {RequestId} registered, awaiting user decision", requestId);

        return AwaitAndCleanupAsync(tcs.Task, registration);
    }

    private static async Task<ApprovalDecision> AwaitAndCleanupAsync(
        Task<ApprovalDecision> task,
        CancellationTokenRegistration registration)
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        finally
        {
            registration.Dispose();
        }
    }

    public bool TryResolve(Guid requestId, ApprovalDecision decision)
    {
        if (!_pending.TryRemove(requestId, out var tcs))
        {
            _logger.LogWarning("Approval resolution for unknown request {RequestId}", requestId);
            return false;
        }

        _logger.LogInformation(
            "Approval request {RequestId} resolved: Approved={Approved}, RememberForSession={Remember}",
            requestId, decision.Approved, decision.RememberForSession);

        return tcs.TrySetResult(decision);
    }

    public void RememberApproval(Guid sessionId, string toolName)
    {
        if (string.IsNullOrEmpty(toolName)) return;
        var bag = _rememberedBySession.GetOrAdd(sessionId, _ => new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase));
        bag[toolName] = 1;
    }

    public bool IsToolApprovedForSession(Guid sessionId, string toolName)
    {
        if (string.IsNullOrEmpty(toolName)) return false;
        return _rememberedBySession.TryGetValue(sessionId, out var bag)
               && bag.ContainsKey(toolName);
    }

    public void ForgetSession(Guid sessionId)
    {
        _rememberedBySession.TryRemove(sessionId, out _);
    }
}
