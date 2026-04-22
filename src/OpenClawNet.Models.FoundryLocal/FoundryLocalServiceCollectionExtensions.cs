using Microsoft.Extensions.DependencyInjection;
using OpenClawNet.Models.Abstractions;

namespace OpenClawNet.Models.FoundryLocal;

public static class FoundryLocalServiceCollectionExtensions
{
    public static IServiceCollection AddFoundryLocal(this IServiceCollection services, Action<FoundryLocalOptions>? configure = null)
    {
        if (configure is not null)
            services.Configure(configure);
        else
            services.Configure<FoundryLocalOptions>(_ => { });

        services.AddSingleton<FoundryLocalModelClient>();
        services.AddSingleton<IModelClient>(sp => sp.GetRequiredService<FoundryLocalModelClient>());

        return services;
    }
}
