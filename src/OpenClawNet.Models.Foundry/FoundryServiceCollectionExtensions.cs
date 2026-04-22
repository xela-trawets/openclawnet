using Microsoft.Extensions.DependencyInjection;
using OpenClawNet.Models.Abstractions;

namespace OpenClawNet.Models.Foundry;

public static class FoundryServiceCollectionExtensions
{
    public static IServiceCollection AddFoundry(this IServiceCollection services, Action<FoundryOptions>? configure = null)
    {
        if (configure is not null)
            services.Configure(configure);
        else
            services.Configure<FoundryOptions>(_ => { });

        services.AddHttpClient<FoundryModelClient>();
        services.AddSingleton<IModelClient>(sp => sp.GetRequiredService<FoundryModelClient>());

        return services;
    }
}
