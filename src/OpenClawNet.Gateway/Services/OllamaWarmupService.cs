using OpenClawNet.Models.Abstractions;

namespace OpenClawNet.Gateway.Services;

/// <summary>
/// Warms up the Ollama model at Gateway startup so the first user request
/// is not slow due to model loading (cold start can take 10–30 seconds).
/// </summary>
public sealed class OllamaWarmupService : BackgroundService
{
    private readonly IModelClient _modelClient;
    private readonly ILogger<OllamaWarmupService> _logger;

    public OllamaWarmupService(IModelClient modelClient, ILogger<OllamaWarmupService> logger)
    {
        _modelClient = modelClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Wait briefly for the gateway to fully initialize before sending warmup
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

            _logger.LogInformation("Warming up Ollama model ({Provider})...", _modelClient.ProviderName);

            const int maxAttempts = 5;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await _modelClient.CompleteAsync(new ChatRequest
                    {
                        Messages = [new ChatMessage { Role = ChatMessageRole.User, Content = "hi" }]
                    }, stoppingToken);

                    _logger.LogInformation("Ollama warmup complete — model is ready.");
                    return;
                }
                catch (OperationCanceledException)
                {
                    return; // Host is stopping — not an error
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Ollama warmup attempt {Attempt}/{Max} failed: {Message}",
                        attempt, maxAttempts, ex.Message);

                    if (attempt < maxAttempts)
                    {
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(5 * attempt), stoppingToken);
                        }
                        catch (OperationCanceledException)
                        {
                            return; // Host is stopping during retry backoff — not an error
                        }
                    }
                }
            }

            _logger.LogWarning("Ollama warmup failed after {Max} attempts — first user request may be slow.", maxAttempts);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown before warmup completed — not an error
        }
    }
}
