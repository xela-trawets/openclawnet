using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenClawNet.Models.Abstractions;

namespace OpenClawNet.Models.Ollama;

public static class OllamaServiceCollectionExtensions
{
    public static IServiceCollection AddOllama(this IServiceCollection services, Action<OllamaOptions>? configure = null)
    {
        if (configure is not null)
            services.Configure(configure);

        // Bind OllamaOptions from Model:* config section, then override endpoint from
        // Aspire-injected ConnectionStrings:ollama (set when running with the AppHost).
        // Aspire may inject the connection string as a plain URL ("http://host:port") or
        // in key-value format ("Endpoint=http://host:port") — handle both.
        services.AddOptions<OllamaOptions>()
            .Configure<IConfiguration>((opts, config) =>
            {
                config.GetSection("Model").Bind(opts);

                var aspireConnStr = config.GetConnectionString("ollama");
                if (!string.IsNullOrEmpty(aspireConnStr))
                {
                    if (Uri.TryCreate(aspireConnStr, UriKind.Absolute, out _))
                    {
                        opts.Endpoint = aspireConnStr;
                    }
                    else
                    {
                        // Parse "Endpoint=http://host:port[;...]" format
                        var cb = new DbConnectionStringBuilder { ConnectionString = aspireConnStr };
                        if (cb.TryGetValue("Endpoint", out var ep) && ep?.ToString() is { Length: > 0 } epStr)
                            opts.Endpoint = epStr;
                    }
                }
            });

        // Configure BaseAddress here so the constructor never throws on an invalid URI.
        services.AddHttpClient<OllamaModelClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
            if (Uri.TryCreate(opts.Endpoint, UriKind.Absolute, out var uri))
                client.BaseAddress = uri;
        });
        services.AddSingleton<IModelClient>(sp => sp.GetRequiredService<OllamaModelClient>());

        return services;
    }
}
