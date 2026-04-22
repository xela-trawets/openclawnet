using OpenClawNet.Mcp.Abstractions;

namespace OpenClawNet.Mcp.Core;

/// <summary>
/// Singleton aggregator over every <see cref="IBundledMcpServerRegistration"/>
/// in DI. <see cref="CompositeMcpServerCatalog"/> reads from this so the bundled
/// servers appear alongside DB-backed ones.
/// </summary>
public sealed class BundledMcpServerRegistry
{
    private readonly IReadOnlyList<IBundledMcpServerRegistration> _registrations;

    public BundledMcpServerRegistry(IEnumerable<IBundledMcpServerRegistration> registrations)
    {
        _registrations = registrations.ToList();
    }

    public IReadOnlyList<IBundledMcpServerRegistration> All => _registrations;

    public IReadOnlyList<McpServerDefinition> Definitions => _registrations
        .Select(r => r.Definition)
        .ToList();
}
