using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace OpenClawNet.Adapters.Teams;

/// <summary>
/// DI extension methods for the Teams bot adapter.
/// </summary>
public static class TeamsAdapterExtensions
{
    /// <summary>
    /// Registers the Microsoft Teams bot adapter and all required dependencies.
    /// Requires IAgentOrchestrator to already be registered (via AddAgentRuntime()).
    /// </summary>
    /// <remarks>
    /// Required appsettings.json configuration:
    /// <code>
    /// "Teams": {
    ///   "Enabled": true,
    ///   "MicrosoftAppId": "&lt;your-bot-app-id&gt;",
    ///   "MicrosoftAppPassword": "&lt;your-bot-client-secret&gt;",
    ///   "MicrosoftAppTenantId": ""   // leave empty for multi-tenant
    /// }
    /// </code>
    /// </remarks>
    public static IServiceCollection AddTeamsAdapter(this IServiceCollection services)
    {
        // Bot Framework authentication reads MicrosoftAppId/Password from IConfiguration automatically.
        services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();
        services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();

        // Bot logic — transient so each turn gets a fresh instance.
        services.AddTransient<IBot, OpenClawNetBot>();

        // Our IBotAdapter abstraction wrapping the CloudAdapter + IBot.
        services.AddSingleton<IBotAdapter, TeamsAdapter>();

        return services;
    }
}
