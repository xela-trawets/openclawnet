namespace OpenClawNet.Mcp.Abstractions;

/// <summary>
/// Persisted configuration for a single MCP server.
/// Mirrored at the storage layer by an EF entity.
/// </summary>
public sealed class McpServerDefinition
{
    /// <summary>Stable identifier — used as the prefix in tool names (<c>id.tool</c>).</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Human-readable display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>How OpenClawNet talks to this server.</summary>
    public McpTransport Transport { get; set; } = McpTransport.InProcess;

    // ---- Stdio transport fields ------------------------------------------------

    /// <summary>Executable to launch (stdio transport only).</summary>
    public string? Command { get; set; }

    /// <summary>Command-line arguments (stdio transport only).</summary>
    public string[] Args { get; set; } = Array.Empty<string>();

    /// <summary>
    /// JSON-encoded environment variables for the spawned process.
    /// Stored as ciphertext via <see cref="ISecretStore"/> — never read raw from the DB.
    /// </summary>
    public string? EnvJson { get; set; }

    // ---- HTTP transport fields -------------------------------------------------

    /// <summary>Base URL (HTTP transport only).</summary>
    public string? Url { get; set; }

    /// <summary>
    /// JSON-encoded HTTP headers (e.g. auth tokens) for HTTP transport.
    /// Stored as ciphertext via <see cref="ISecretStore"/>.
    /// </summary>
    public string? HeadersJson { get; set; }

    // ---- State -----------------------------------------------------------------

    /// <summary>Whether the lifecycle service should start this server.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Built-in servers ship with OpenClawNet. They can be disabled but never deleted —
    /// the destructive seed migration in PR-E reasserts them.
    /// </summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>Last error captured while starting or talking to this server.</summary>
    public string? LastError { get; set; }

    /// <summary>Last time this server reported alive.</summary>
    public DateTime? LastSeenUtc { get; set; }
}
