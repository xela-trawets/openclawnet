using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using OpenClawNet.Mcp.Abstractions;

namespace OpenClawNet.Mcp.Core;

/// <summary>
/// DPAPI-backed <see cref="ISecretStore"/>. Encrypts under the current Windows user.
/// </summary>
/// <remarks>
/// On non-Windows platforms DPAPI is unavailable. We fall back to a pass-through
/// (base64-encoded plaintext) and log a loud warning on first use so it can't slip
/// silently past code review. Replace with a cross-platform keyring before shipping
/// production builds for non-Windows hosts.
/// </remarks>
public sealed class DpapiSecretStore : ISecretStore
{
    private const string PassthroughPrefix = "ocn-plain:";
    private static readonly byte[] s_entropy = Encoding.UTF8.GetBytes("OpenClawNet.Mcp.SecretStore.v1");

    private readonly ILogger<DpapiSecretStore> _logger;
    private int _passthroughWarned;

    public DpapiSecretStore(ILogger<DpapiSecretStore> logger)
    {
        _logger = logger;
    }

    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            WarnPassthroughOnce();
            return PassthroughPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext));
        }

        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var encrypted = ProtectedData.Protect(bytes, s_entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    public string? Unprotect(string ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);

        if (ciphertext.StartsWith(PassthroughPrefix, StringComparison.Ordinal))
        {
            try
            {
                var raw = Convert.FromBase64String(ciphertext[PassthroughPrefix.Length..]);
                return Encoding.UTF8.GetString(raw);
            }
            catch (FormatException ex)
            {
                _logger.LogWarning(ex, "DpapiSecretStore: passthrough payload was not valid base64.");
                return null;
            }
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _logger.LogWarning("DpapiSecretStore: cannot decrypt DPAPI payload on a non-Windows platform.");
            return null;
        }

        try
        {
            var encrypted = Convert.FromBase64String(ciphertext);
            var bytes = ProtectedData.Unprotect(encrypted, s_entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex, "DpapiSecretStore: failed to decrypt secret. Was it encrypted by a different user?");
            return null;
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "DpapiSecretStore: ciphertext was not valid base64.");
            return null;
        }
    }

    private void WarnPassthroughOnce()
    {
        if (Interlocked.Exchange(ref _passthroughWarned, 1) == 0)
        {
            _logger.LogWarning(
                "DpapiSecretStore: running on a non-Windows platform — secrets will be base64-encoded but NOT encrypted. " +
                "This is acceptable for development only. Replace with a cross-platform keyring before production deployment.");
        }
    }
}
