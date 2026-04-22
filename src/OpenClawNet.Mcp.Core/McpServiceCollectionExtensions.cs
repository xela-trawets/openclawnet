using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenClawNet.Mcp.Abstractions;

namespace OpenClawNet.Mcp.Core;

/// <summary>
/// DI helper that wires up the MCP foundation: secret store, hosts, tool provider,
/// and the lifecycle service that starts every enabled server on app startup.
/// </summary>
/// <remarks>
/// <see cref="IMcpServerCatalog"/> is intentionally NOT registered here — the storage
/// layer owns that registration so the Mcp.Core assembly stays free of EF Core.
/// </remarks>
public static class McpServiceCollectionExtensions
{
    public static IServiceCollection AddOpenClawMcp(this IServiceCollection services)
    {
        services.TryAddSingleton<ISecretStore, DpapiSecretStore>();
        services.TryAddSingleton<InProcessMcpHost>();
        services.TryAddSingleton<StdioMcpHost>();
        services.TryAddSingleton<IMcpToolProvider, McpToolProvider>();
        services.AddHostedService<McpServerLifecycleService>();
        return services;
    }

    /// <summary>
    /// Wires the bundled MCP server registry, decorates the existing
    /// <see cref="IMcpServerCatalog"/> registration with <see cref="CompositeMcpServerCatalog"/>,
    /// and registers <see cref="BundledMcpStartupService"/> so each
    /// <see cref="IBundledMcpServerRegistration"/> registered in DI starts on app boot.
    /// </summary>
    /// <remarks>
    /// Must be called AFTER both <c>AddOpenClawStorage()</c> (which registers the underlying
    /// <see cref="IMcpServerCatalog"/>) and <c>AddOpenClawMcp()</c>.
    /// </remarks>
    public static IServiceCollection AddBundledMcpServers(this IServiceCollection services)
    {
        services.TryAddSingleton<BundledMcpServerRegistry>();

        // Decorate the previously-registered IMcpServerCatalog so bundled defs appear
        // alongside DB rows. Without Scrutor we do the swap manually.
        var existing = services.LastOrDefault(s => s.ServiceType == typeof(IMcpServerCatalog))
            ?? throw new InvalidOperationException(
                "IMcpServerCatalog must be registered before AddBundledMcpServers() is called " +
                "(typically via AddOpenClawStorage()).");

        services.Remove(existing);
        services.AddSingleton<IMcpServerCatalog>(sp =>
        {
            IMcpServerCatalog inner;
            if (existing.ImplementationFactory is not null)
            {
                inner = (IMcpServerCatalog)existing.ImplementationFactory(sp);
            }
            else if (existing.ImplementationInstance is IMcpServerCatalog instance)
            {
                inner = instance;
            }
            else if (existing.ImplementationType is not null)
            {
                inner = (IMcpServerCatalog)ActivatorUtilities.CreateInstance(sp, existing.ImplementationType);
            }
            else
            {
                throw new InvalidOperationException("Could not resolve original IMcpServerCatalog registration.");
            }

            var bundled = sp.GetRequiredService<BundledMcpServerRegistry>();
            return new CompositeMcpServerCatalog(inner, bundled);
        });

        services.AddHostedService<BundledMcpStartupService>();
        return services;
    }
}
