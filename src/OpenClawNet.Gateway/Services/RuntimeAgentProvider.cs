using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenClawNet.Models.Abstractions;

namespace OpenClawNet.Gateway.Services;

/// <summary>
/// Routing <see cref="IAgentProvider"/> that delegates to the provider matching the
/// current <see cref="RuntimeModelSettings"/> (or an explicit profile-level override).
/// Registered as the primary <see cref="IAgentProvider"/> in DI.
/// </summary>
public sealed class RuntimeAgentProvider : IAgentProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RuntimeModelSettings _settings;
    private readonly ILogger<RuntimeAgentProvider> _logger;
    private IReadOnlyList<IAgentProvider>? _providers;

    public RuntimeAgentProvider(
        IServiceProvider serviceProvider,
        RuntimeModelSettings settings,
        ILogger<RuntimeAgentProvider> logger)
    {
        _serviceProvider = serviceProvider;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Lazily resolves the other providers to break the circular dependency
    /// (RuntimeAgentProvider is itself registered as IAgentProvider).
    /// </summary>
    private IReadOnlyList<IAgentProvider> Providers =>
        _providers ??= _serviceProvider.GetServices<IAgentProvider>()
            .Where(p => p is not RuntimeAgentProvider)
            .ToList();

    public string ProviderName => GetActiveProvider().ProviderName;

    public IChatClient CreateChatClient(AgentProfile profile)
    {
        var providerName = profile.Provider ?? _settings.Current.Provider;
        var provider = FindProvider(providerName);
        _logger.LogInformation("Creating chat client via {Provider} for profile {Profile}", providerName, profile.Name);
        return provider.CreateChatClient(profile);
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => GetActiveProvider().IsAvailableAsync(ct);

    private IAgentProvider GetActiveProvider()
        => FindProvider(_settings.Current.Provider);

    private IAgentProvider FindProvider(string name)
    {
        var provider = Providers.FirstOrDefault(p =>
            p.ProviderName.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
            throw new InvalidOperationException(
                $"No agent provider registered for '{name}'. Available: {string.Join(", ", Providers.Select(p => p.ProviderName))}");

        return provider;
    }
}
