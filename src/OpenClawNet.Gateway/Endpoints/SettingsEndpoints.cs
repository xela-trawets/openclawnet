using OpenClawNet.Gateway.Services;

namespace OpenClawNet.Gateway.Endpoints;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/settings").WithTags("Settings");

        // GET /api/settings — returns the current model provider configuration.
        // The ApiKey is masked (never returned to the UI for security).
        group.MapGet("/", (RuntimeModelSettings settings) =>
        {
            var cfg = settings.Current;
            // For Azure OpenAI, the "model" shown to users is DeploymentName, not Model.
            // Model is Ollama-specific; DeploymentName is the Azure concept.
            var displayModel = cfg.Provider.Equals("azure-openai", StringComparison.OrdinalIgnoreCase)
                ? cfg.DeploymentName ?? cfg.Model
                : cfg.Model;
            return Results.Ok(new SettingsResponse(
                Provider:       cfg.Provider,
                Model:          displayModel,
                Endpoint:       cfg.Endpoint,
                DeploymentName: cfg.DeploymentName,
                AuthMode:       cfg.AuthMode ?? "api-key",
                HasApiKey:      !string.IsNullOrEmpty(cfg.ApiKey),
                FoundryProjectEndpoint: cfg.FoundryProjectEndpoint,
                FoundryAuthMode:        cfg.FoundryAuthMode,
                CopilotEnabled:         cfg.CopilotEnabled,
                CopilotModel:           cfg.CopilotModel
            ));
        })
        .WithName("GetSettings")
        .WithDescription("Returns the current model provider configuration");

        // PUT /api/settings — updates the active model provider at runtime.
        // If the caller does not include an ApiKey and HasApiKey was true before, the old key is preserved.
        group.MapPut("/", (SettingsRequest request, RuntimeModelSettings settings) =>
        {
            var previous = settings.Current;

            var updated = new ModelProviderConfig
            {
                Provider       = request.Provider,
                Model          = NullIfEmpty(request.Model),
                Endpoint       = NullIfEmpty(request.Endpoint),
                DeploymentName = NullIfEmpty(request.DeploymentName),
                AuthMode       = NullIfEmpty(request.AuthMode) ?? "api-key",
                // Preserve the existing API key when the caller sends an empty/null value
                // (the UI masks it and the user may not re-enter it on every save)
                ApiKey         = string.IsNullOrEmpty(request.ApiKey)
                                     ? previous.ApiKey
                                     : request.ApiKey,
                FoundryProjectEndpoint = NullIfEmpty(request.FoundryProjectEndpoint),
                FoundryAuthMode        = NullIfEmpty(request.FoundryAuthMode),
                CopilotEnabled         = request.CopilotEnabled ?? previous.CopilotEnabled,
                CopilotModel           = NullIfEmpty(request.CopilotModel),
            };

            settings.Update(updated);

            return Results.Ok(new SettingsResponse(
                Provider:       updated.Provider,
                Model:          updated.Model,
                Endpoint:       updated.Endpoint,
                DeploymentName: updated.DeploymentName,
                AuthMode:       updated.AuthMode ?? "api-key",
                HasApiKey:      !string.IsNullOrEmpty(updated.ApiKey),
                FoundryProjectEndpoint: updated.FoundryProjectEndpoint,
                FoundryAuthMode:        updated.FoundryAuthMode,
                CopilotEnabled:         updated.CopilotEnabled,
                CopilotModel:           updated.CopilotModel
            ));
        })
        .WithName("UpdateSettings")
        .WithDescription("Updates the active model provider configuration without requiring a restart");
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record SettingsResponse(
    string Provider,
    string? Model,
    string? Endpoint,
    string? DeploymentName,
    string AuthMode,
    bool HasApiKey,
    string? FoundryProjectEndpoint,
    string? FoundryAuthMode,
    bool CopilotEnabled,
    string? CopilotModel);

public sealed record SettingsRequest(
    string Provider,
    string? Model,
    string? Endpoint,
    string? ApiKey,
    string? DeploymentName,
    string? AuthMode,
    string? FoundryProjectEndpoint,
    string? FoundryAuthMode,
    bool? CopilotEnabled,
    string? CopilotModel);
