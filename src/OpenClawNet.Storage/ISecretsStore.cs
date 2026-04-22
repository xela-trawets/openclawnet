namespace OpenClawNet.Storage;

/// <summary>
/// CRUD over the encrypted Secrets table. Plaintext values never round-trip to
/// disk; the implementation handles DataProtection encryption transparently.
/// </summary>
public interface ISecretsStore
{
    /// <summary>Returns the plaintext value, or <c>null</c> when the secret is not present.</summary>
    Task<string?> GetAsync(string name, CancellationToken ct = default);

    /// <summary>Lists secret names + descriptions (no values returned, by design).</summary>
    Task<IReadOnlyList<SecretSummary>> ListAsync(CancellationToken ct = default);

    /// <summary>Insert or update a secret. The plaintext is encrypted before persistence.</summary>
    Task SetAsync(string name, string value, string? description = null, CancellationToken ct = default);

    /// <summary>Removes a secret by name. Returns true when a row was deleted.</summary>
    Task<bool> DeleteAsync(string name, CancellationToken ct = default);
}

/// <summary>Metadata-only projection (no plaintext value) suitable for UI listings.</summary>
public sealed record SecretSummary(string Name, string? Description, DateTime UpdatedAt);
