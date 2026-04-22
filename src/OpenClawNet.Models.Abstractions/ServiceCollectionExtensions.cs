using Microsoft.Extensions.DependencyInjection;

namespace OpenClawNet.Models.Abstractions;

public static class ModelServiceCollectionExtensions
{
    public static IServiceCollection AddModelClient<TClient>(this IServiceCollection services)
        where TClient : class, IModelClient
    {
        services.AddSingleton<IModelClient, TClient>();
        return services;
    }
}
