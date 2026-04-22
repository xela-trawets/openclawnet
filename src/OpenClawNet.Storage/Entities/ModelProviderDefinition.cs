namespace OpenClawNet.Storage.Entities;

/// <summary>
/// A named model provider configuration. Multiple definitions can exist for the same provider type
/// (e.g., "ollama-gemma" and "ollama-llama" both with ProviderType "ollama").
/// </summary>
public sealed class ModelProviderDefinition
{
    /// <summary>Unique name, e.g. "ollama-gemma", "azure-gpt4o".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Provider type: "ollama", "azure-openai", "foundry", "foundry-local", "github-copilot", "lm-studio".</summary>
    public string ProviderType { get; set; } = string.Empty;

    /// <summary>Display name shown in UI.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Endpoint URL (for Ollama, Azure OpenAI, Foundry).</summary>
    public string? Endpoint { get; set; }

    /// <summary>Model name or deployment name.</summary>
    public string? Model { get; set; }

    /// <summary>API key (Azure OpenAI, Foundry).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Azure OpenAI deployment name.</summary>
    public string? DeploymentName { get; set; }

    /// <summary>"api-key" or "integrated" (Azure OpenAI, Foundry).</summary>
    public string? AuthMode { get; set; }

    /// <summary>Whether this provider definition has been tested and confirmed working.</summary>
    public bool IsSupported { get; set; }

    /// <summary>When this provider was last tested.</summary>
    public DateTime? LastTestedAt { get; set; }

    /// <summary>Result of the last test (null = never tested, true = success, false = failure).</summary>
    public bool? LastTestSucceeded { get; set; }

    /// <summary>Error message from the last test (populated only when LastTestSucceeded is false).</summary>
    public string? LastTestError { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
