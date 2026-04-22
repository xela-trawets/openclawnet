using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ElBruno.LocalEmbeddings.Extensions;

namespace OpenClawNet.Memory;

public static class MemoryServiceCollectionExtensions
{
    public static IServiceCollection AddMemory(this IServiceCollection services, IConfiguration? configuration = null)
    {
        services.AddScoped<IMemoryService, DefaultMemoryService>();
        services.AddScoped<IEmbeddingsService, DefaultEmbeddingsService>();

        if (configuration is not null)
        {
            services.AddLocalEmbeddings(configuration.GetSection("LocalEmbeddings"));
        }
        else
        {
            services.AddLocalEmbeddings(_ => { });
        }

        return services;
    }
}

