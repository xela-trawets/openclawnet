namespace OpenClawNet.Storage.Entities;

/// <summary>
/// Append-only timeline of what happened during a single <see cref="JobRun"/>.
/// Survives app restarts, unlike OTEL traces. One row per agent message,
/// tool call, or terminal status transition.
/// </summary>
public sealed class JobRunEvent
{
    /// <summary>Maximum bytes persisted for any single args/result blob.</summary>
    /// <remarks>
    /// Larger payloads are truncated with a "...[truncated N bytes]" suffix to keep
    /// the table from ballooning on tools like image_edit / embeddings that return
    /// large binary or vector results. References (file paths, URLs) should be
    /// preferred by tools — this is a defensive cap, not a primary mechanism.
    /// </remarks>
    public const int MaxPayloadBytes = 4 * 1024;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid JobRunId { get; set; }

    /// <summary>0-based monotonic order within the run.</summary>
    public int Sequence { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>One of <see cref="JobRunEventKind"/>. Stored as a lowercase string for forward compatibility.</summary>
    public string Kind { get; set; } = JobRunEventKind.AgentStarted;

    /// <summary>Tool name for <c>tool_call</c> events; null otherwise.</summary>
    public string? ToolName { get; set; }

    /// <summary>JSON-serialized arguments passed to the tool. Truncated at <see cref="MaxPayloadBytes"/>.</summary>
    public string? ArgumentsJson { get; set; }

    /// <summary>JSON-serialized tool result. Truncated at <see cref="MaxPayloadBytes"/>.</summary>
    public string? ResultJson { get; set; }

    /// <summary>Free-form message (e.g. final response, error message).</summary>
    public string? Message { get; set; }

    /// <summary>Duration of the step in milliseconds, when applicable.</summary>
    public int? DurationMs { get; set; }

    /// <summary>Tokens contributed by this step (for model calls).</summary>
    public int? TokensUsed { get; set; }

    public JobRun Run { get; set; } = null!;

    /// <summary>
    /// Truncates a payload to <see cref="MaxPayloadBytes"/> bytes, appending a
    /// human-readable suffix that makes the truncation visible in the UI.
    /// </summary>
    public static string? Truncate(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (value.Length <= MaxPayloadBytes) return value;
        var dropped = value.Length - MaxPayloadBytes;
        return string.Concat(value.AsSpan(0, MaxPayloadBytes), $"...[truncated {dropped} chars]");
    }
}

/// <summary>
/// Canonical event kinds recognised by the UI. Stored as strings so new kinds
/// can be added without a schema migration.
/// </summary>
public static class JobRunEventKind
{
    public const string AgentStarted     = "agent_started";
    public const string ToolCall         = "tool_call";
    public const string AgentCompleted   = "agent_completed";
    public const string AgentFailed      = "agent_failed";
    public const string ProfileRefused   = "profile_refused";
}
