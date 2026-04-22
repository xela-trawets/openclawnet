using Microsoft.EntityFrameworkCore;
using OpenClawNet.Models.Abstractions;
using OpenClawNet.Storage.Entities;

namespace OpenClawNet.Storage;

/// <summary>
/// EF Core-backed implementation of <see cref="IModelProviderDefinitionStore"/>.
/// </summary>
public sealed class ModelProviderDefinitionStore : IModelProviderDefinitionStore
{
    private readonly IDbContextFactory<OpenClawDbContext> _dbFactory;

    public ModelProviderDefinitionStore(IDbContextFactory<OpenClawDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<ModelProviderDefinition?> GetAsync(string name, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.ModelProviders.FindAsync([name], ct);
    }

    public async Task<IReadOnlyList<ModelProviderDefinition>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.ModelProviders.OrderBy(d => d.Name).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ModelProviderDefinition>> ListByTypeAsync(string providerType, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.ModelProviders
            .Where(d => d.ProviderType == providerType)
            .OrderBy(d => d.Name)
            .ToListAsync(ct);
    }

    public async Task SaveAsync(ModelProviderDefinition definition, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var existing = await db.ModelProviders.FindAsync([definition.Name], ct);

        if (existing is not null)
        {
            existing.ProviderType = definition.ProviderType;
            existing.DisplayName = definition.DisplayName;
            existing.Endpoint = definition.Endpoint;
            existing.Model = definition.Model;
            existing.ApiKey = definition.ApiKey;
            existing.DeploymentName = definition.DeploymentName;
            existing.AuthMode = definition.AuthMode;
            existing.IsSupported = definition.IsSupported;
            existing.UpdatedAt = definition.UpdatedAt;
            existing.LastTestedAt = definition.LastTestedAt;
            existing.LastTestSucceeded = definition.LastTestSucceeded;
            existing.LastTestError = definition.LastTestError;
        }
        else
        {
            db.ModelProviders.Add(definition);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string name, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.ModelProviders.FindAsync([name], ct);
        if (entity is not null)
        {
            db.ModelProviders.Remove(entity);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task SeedDefaultsAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        if (await db.ModelProviders.AnyAsync(ct))
            return;

        var defaults = new[]
        {
            new ModelProviderDefinition
            {
                Name = "ollama-default",
                ProviderType = ProviderTypeDefaults.Ollama.ProviderType,
                DisplayName = ProviderTypeDefaults.Ollama.DisplayName,
                Endpoint = ProviderTypeDefaults.Ollama.Endpoint,
                Model = ProviderTypeDefaults.Ollama.Model,
                IsSupported = false
            },
            new ModelProviderDefinition
            {
                Name = "azure-openai-default",
                ProviderType = ProviderTypeDefaults.AzureOpenAI.ProviderType,
                DisplayName = ProviderTypeDefaults.AzureOpenAI.DisplayName,
                DeploymentName = ProviderTypeDefaults.AzureOpenAI.DeploymentName,
                AuthMode = ProviderTypeDefaults.AzureOpenAI.AuthMode,
                IsSupported = false
            },
            new ModelProviderDefinition
            {
                Name = "github-copilot-default",
                ProviderType = ProviderTypeDefaults.GitHubCopilot.ProviderType,
                DisplayName = ProviderTypeDefaults.GitHubCopilot.DisplayName,
                IsSupported = false
            },
            new ModelProviderDefinition
            {
                Name = "foundry-default",
                ProviderType = ProviderTypeDefaults.Foundry.ProviderType,
                DisplayName = ProviderTypeDefaults.Foundry.DisplayName,
                Model = ProviderTypeDefaults.Foundry.Model,
                AuthMode = ProviderTypeDefaults.Foundry.AuthMode,
                IsSupported = false
            },
            new ModelProviderDefinition
            {
                Name = "foundry-local-default",
                ProviderType = ProviderTypeDefaults.FoundryLocal.ProviderType,
                DisplayName = ProviderTypeDefaults.FoundryLocal.DisplayName,
                Model = ProviderTypeDefaults.FoundryLocal.Model,
                IsSupported = false
            },
            new ModelProviderDefinition
            {
                Name = "lm-studio-default",
                ProviderType = ProviderTypeDefaults.LMStudio.ProviderType,
                DisplayName = ProviderTypeDefaults.LMStudio.DisplayName,
                Endpoint = ProviderTypeDefaults.LMStudio.Endpoint,
                IsSupported = false
            }
        };

        db.ModelProviders.AddRange(defaults);
        await db.SaveChangesAsync(ct);
    }
}
