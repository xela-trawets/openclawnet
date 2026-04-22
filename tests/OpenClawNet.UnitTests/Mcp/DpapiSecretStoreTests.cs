using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenClawNet.Mcp.Core;

namespace OpenClawNet.UnitTests.Mcp;

public class DpapiSecretStoreTests
{
    private static DpapiSecretStore CreateStore() => new(NullLogger<DpapiSecretStore>.Instance);

    [SkippableFact]
    public void Protect_RoundTrips_OnWindows()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "DPAPI is only available on Windows.");

        var store = CreateStore();
        var plaintext = "AKIA-EXAMPLE-NOT-A-REAL-SECRET-12345";

        var protectedValue = store.Protect(plaintext);
        protectedValue.Should().NotBe(plaintext);

        var unprotected = store.Unprotect(protectedValue);
        unprotected.Should().Be(plaintext);
    }

    [SkippableFact]
    public void Protect_PassesThrough_OnNonWindows()
    {
        Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Windows uses DPAPI; non-Windows fallback only.");

        var store = CreateStore();
        var plaintext = "dev-only-secret";

        var protectedValue = store.Protect(plaintext);
        var unprotected = store.Unprotect(protectedValue);
        unprotected.Should().Be(plaintext);
    }

    [Fact]
    public void Unprotect_ReturnsNull_ForGarbage()
    {
        var store = CreateStore();
        var result = store.Unprotect("not-base64-and-not-encrypted");
        result.Should().BeNull();
    }
}
