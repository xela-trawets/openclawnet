namespace OpenClawNet.Models.Abstractions;

/// <summary>
/// Immutable per-request provider configuration resolved from a ModelProviderDefinition
/// or RuntimeModelSettings. Carried through the request pipeline so the runtime
/// knows exactly which provider/endpoint/model to use.
/// </summary>
public sealed record ResolvedProviderConfig
{
    /// <summary>Provider type: "ollama", "azure-openai", "foundry", etc.</summary>
    public required string ProviderType { get; init; }
    public string? Endpoint { get; init; }
    public string? Model { get; init; }
    public string? ApiKey { get; init; }
    public string? DeploymentName { get; init; }
    public string? AuthMode { get; init; }
    /// <summary>The ModelProviderDefinition name this was resolved from (for audit trail).</summary>
    public string? DefinitionName { get; init; }
}
