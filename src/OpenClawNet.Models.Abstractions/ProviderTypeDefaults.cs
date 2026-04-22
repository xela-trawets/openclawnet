namespace OpenClawNet.Models.Abstractions;

/// <summary>
/// Central source of default values for each provider type.
/// Eliminates the 15+ scattered hardcoded defaults across the codebase.
/// Used by ModelProviderDefinitionStore.SeedDefaultsAsync, Options classes,
/// and RuntimeModelSettings.FromConfiguration.
/// </summary>
public static class ProviderTypeDefaults
{
    public static class Ollama
    {
        public const string ProviderType = "ollama";
        public const string Endpoint = "http://localhost:11434";
        public const string Model = "gemma4:e2b";
        public const string DisplayName = "Ollama (Local)";
        public const double Temperature = 0.7;
        public const int MaxTokens = 4096;
    }

    public static class AzureOpenAI
    {
        public const string ProviderType = "azure-openai";
        public const string DeploymentName = "gpt-5-mini";
        public const string AuthMode = "api-key";
        public const string DisplayName = "Azure OpenAI";
    }

    public static class GitHubCopilot
    {
        public const string ProviderType = "github-copilot";
        public const string Model = "gpt-5-mini";
        public const string DisplayName = "GitHub Copilot SDK";
    }

    public static class Foundry
    {
        public const string ProviderType = "foundry";
        public const string Model = "gpt-4o-mini";
        public const string AuthMode = "api-key";
        public const string DisplayName = "Microsoft Foundry";
    }

    public static class FoundryLocal
    {
        public const string ProviderType = "foundry-local";
        public const string Model = "phi-4";
        public const string DisplayName = "Foundry Local";
    }

    public static class LMStudio
    {
        public const string ProviderType = "lm-studio";
        public const string Endpoint = "http://localhost:1234";
        public const string DisplayName = "LM Studio";
    }
}
