using OpenClawNet.Models.Abstractions;
using OpenClawNet.Storage.Entities;

namespace OpenClawNet.Storage;

/// <summary>
/// Persistence interface for <see cref="AgentProfile"/> configurations.
/// </summary>
public interface IAgentProfileStore
{
    Task<AgentProfile?> GetAsync(string name, CancellationToken ct = default);
    Task<AgentProfile> GetDefaultAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AgentProfile>> ListAsync(CancellationToken ct = default);
    Task SaveAsync(AgentProfile profile, CancellationToken ct = default);
    Task DeleteAsync(string name, CancellationToken ct = default);
    
    /// <summary>Gets the underlying entity for test result tracking.</summary>
    Task<AgentProfileEntity?> GetEntityAsync(string name, CancellationToken ct = default);
    
    /// <summary>Saves only test result fields on the entity without touching other fields.</summary>
    Task SaveEntityAsync(AgentProfileEntity entity, CancellationToken ct = default);
}
