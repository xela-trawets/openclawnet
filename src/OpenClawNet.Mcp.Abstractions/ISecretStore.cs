namespace OpenClawNet.Mcp.Abstractions;

/// <summary>
/// Encrypts and decrypts secrets persisted to the OpenClawNet database
/// (MCP server env vars, HTTP headers, future API keys).
/// </summary>
/// <remarks>
/// This abstraction exists so the v1 DPAPI-on-Windows implementation can be swapped
/// for a cross-platform keyring in a future release without a breaking change.
/// </remarks>
public interface ISecretStore
{
    /// <summary>Encrypt a plaintext secret. Returns ciphertext suitable for storage.</summary>
    string Protect(string plaintext);

    /// <summary>
    /// Decrypt a ciphertext value previously returned by <see cref="Protect"/>.
    /// Returns <see langword="null"/> if the value cannot be decrypted on this machine.
    /// </summary>
    string? Unprotect(string ciphertext);
}
