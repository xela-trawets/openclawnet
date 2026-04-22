namespace OpenClawNet.Storage.Entities;

/// <summary>
/// EF Core entity for <see cref="Mcp.Abstractions.McpServerDefinition"/>.
/// Encrypted columns (<see cref="EnvJson"/>, <see cref="HeadersJson"/>) carry
/// ciphertext from <see cref="Mcp.Abstractions.ISecretStore"/> and are never
/// decrypted at the storage boundary — service-layer code does that on read.
/// </summary>
public class McpServerDefinitionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    /// <summary>Stored as the enum's name (InProcess|Stdio|Http).</summary>
    public string Transport { get; set; } = "InProcess";

    public string? Command { get; set; }

    /// <summary>JSON-encoded <c>string[]</c>. Empty-string when there are no args.</summary>
    public string ArgsJson { get; set; } = "[]";

    /// <summary>Encrypted JSON of env-var dictionary.</summary>
    public string? EnvJson { get; set; }

    public string? Url { get; set; }

    /// <summary>Encrypted JSON of HTTP-header dictionary.</summary>
    public string? HeadersJson { get; set; }

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Built-in servers ship with OpenClawNet. They can be disabled but never deleted —
    /// PR-E's destructive seed migration reasserts them.
    /// </summary>
    public bool IsBuiltIn { get; set; }

    public string? LastError { get; set; }
    public DateTime? LastSeenUtc { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
