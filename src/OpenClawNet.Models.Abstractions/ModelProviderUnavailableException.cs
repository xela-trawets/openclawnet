namespace OpenClawNet.Models.Abstractions;

/// <summary>
/// Thrown when the model provider (Ollama, Azure OpenAI, Foundry, etc.) is unreachable
/// or otherwise unable to serve requests. Endpoints should translate this to HTTP 503.
/// </summary>
public sealed class ModelProviderUnavailableException : Exception
{
    public string ProviderName { get; }

    public ModelProviderUnavailableException(string providerName, string message, Exception innerException)
        : base(message, innerException)
    {
        ProviderName = providerName;
    }

    public ModelProviderUnavailableException(string providerName, string message)
        : base(message)
    {
        ProviderName = providerName;
    }
}
