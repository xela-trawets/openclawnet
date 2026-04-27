using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace OpenClawNet.Storage;

public static class StorageServiceCollectionExtensions
{
    public static IServiceCollection AddOpenClawStorage(this IServiceCollection services, string? connectionString = null)
    {
        services.AddDbContextFactory<OpenClawDbContext>((sp, options) =>
        {
            // IConfiguration is a singleton — safe to resolve from the root provider.
            // Aspire injects ConnectionStrings:openclawnet-db as an env var; this reads it correctly.
            var config = sp.GetService<IConfiguration>();
            var connStr = config?.GetConnectionString("openclawnet-db")
                ?? connectionString
                ?? "Data Source=openclawnet.db";
            options.UseSqlite(connStr);
        });

        // Bind the local-filesystem layout (root dir, binary artifacts, model cache).
        services.AddOptions<StorageOptions>()
            .Configure<IConfiguration>((opts, cfg) => cfg.GetSection(StorageOptions.SectionName).Bind(opts))
            .PostConfigure(opts => opts.EnsureDirectories());
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<StorageOptions>>().Value);

        services.AddScoped<IConversationStore, ConversationStore>();
        services.AddScoped<IAgentProfileStore, AgentProfileStore>();
        services.AddScoped<IModelProviderDefinitionStore, ModelProviderDefinitionStore>();
        services.AddScoped<IToolTestRecordStore, ToolTestRecordStore>();
        services.AddScoped<ISecretsStore, SecretsStore>();
        services.AddSingleton<OpenClawNet.Mcp.Abstractions.IMcpServerCatalog, McpServerCatalog>();
        services.AddScoped<Services.SkillImportService>();

        return services;
    }
}
