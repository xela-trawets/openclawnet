using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenClawNet.Models.Abstractions;

namespace OpenClawNet.Models.AzureOpenAI;

public static class AzureOpenAIServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Azure OpenAI model client.
    /// Configuration is read from the "Model" config section:
    ///   Model:Endpoint        — Azure OpenAI resource URL (required)
    ///   Model:DeploymentName  — deployment / model name (default: gpt-5-mini)
    ///   Model:AuthMode        — "api-key" (default) or "integrated"
    ///   Model:ApiKey          — required when AuthMode=api-key
    /// </summary>
    public static IServiceCollection AddAzureOpenAI(this IServiceCollection services)
    {
        services.AddOptions<AzureOpenAIOptions>()
            .Configure<IConfiguration>((opts, config) =>
            {
                // Read from Model:* section (consistent with other providers)
                var section = config.GetSection("Model");
                if (section["Endpoint"] is { Length: > 0 } ep)       opts.Endpoint = ep;
                if (section["ApiKey"] is { Length: > 0 } key)         opts.ApiKey = key;
                if (section["DeploymentName"] is { Length: > 0 } dep) opts.DeploymentName = dep;
                if (section["AuthMode"] is { Length: > 0 } mode)      opts.AuthMode = mode;

                // Also support AzureOpenAI:* flat keys (handy for user secrets)
                if (config["AzureOpenAI:Endpoint"] is { Length: > 0 } ep2)       opts.Endpoint = ep2;
                if (config["AzureOpenAI:ApiKey"] is { Length: > 0 } key2)         opts.ApiKey = key2;
                if (config["AzureOpenAI:DeploymentName"] is { Length: > 0 } dep2) opts.DeploymentName = dep2;
                if (config["AzureOpenAI:AuthMode"] is { Length: > 0 } mode2)      opts.AuthMode = mode2;
            });

        services.AddSingleton<AzureOpenAIModelClient>();
        services.AddSingleton<IModelClient>(sp => sp.GetRequiredService<AzureOpenAIModelClient>());

        return services;
    }
}
