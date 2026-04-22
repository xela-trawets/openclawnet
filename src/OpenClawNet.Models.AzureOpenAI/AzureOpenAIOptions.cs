namespace OpenClawNet.Models.AzureOpenAI;

/// <summary>
/// Configuration options for the Azure OpenAI model provider.
/// Supports two authentication modes:
///   api-key  (default): Endpoint + ApiKey + DeploymentName
///   integrated:         Endpoint + DeploymentName (uses DefaultAzureCredential — managed identity, Azure CLI, VS, etc.)
/// </summary>
public sealed class AzureOpenAIOptions
{
    /// <summary>
    /// Azure OpenAI resource endpoint, e.g. https://my-resource.openai.azure.com/
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// API key. Required when AuthMode is "api-key".
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Deployment / model name in Azure OpenAI, e.g. "gpt-5-mini", "gpt-4o", "gpt-4".
    /// </summary>
    public string DeploymentName { get; set; } = "gpt-5-mini";

    /// <summary>
    /// Authentication mode: "api-key" (default) or "integrated" (DefaultAzureCredential).
    /// </summary>
    public string AuthMode { get; set; } = "api-key";

    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 4096;
}
