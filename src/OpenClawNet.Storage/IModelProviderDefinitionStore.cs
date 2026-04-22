using OpenClawNet.Storage.Entities;

namespace OpenClawNet.Storage;

public interface IModelProviderDefinitionStore
{
    Task<ModelProviderDefinition?> GetAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<ModelProviderDefinition>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ModelProviderDefinition>> ListByTypeAsync(string providerType, CancellationToken ct = default);
    Task SaveAsync(ModelProviderDefinition definition, CancellationToken ct = default);
    Task DeleteAsync(string name, CancellationToken ct = default);
    Task SeedDefaultsAsync(CancellationToken ct = default);
}
