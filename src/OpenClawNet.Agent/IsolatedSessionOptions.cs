namespace OpenClawNet.Agent;

/// <summary>
/// Options that configure the behavior of an isolated agent session.
/// Isolated sessions start with a clean context — no history from other sessions.
/// </summary>
/// <param name="Purpose">
///     A short label describing why the isolated session was created.
///     Conventional values: <c>"job"</c>, <c>"webhook"</c>, <c>"test"</c>.
/// </param>
/// <param name="PersistMessages">
///     When <see langword="false"/> (default), messages are kept in-memory only and
///     are never written to the conversation database. Set to <see langword="true"/>
///     to record the isolated session in the persistent store.
/// </param>
/// <param name="TtlSeconds">
///     Optional time-to-live in seconds. Reserved for future cleanup scheduling;
///     not currently enforced by the runtime.
/// </param>
/// <param name="WorkspacePath">
///     Optional override for the workspace directory used to load bootstrap files
///     (AGENTS.md, SOUL.md, USER.md). When <see langword="null"/>, the globally
///     configured workspace path is used.
/// </param>
public sealed record IsolatedSessionOptions(
    string Purpose,
    bool PersistMessages = false,
    int? TtlSeconds = null,
    string? WorkspacePath = null);
