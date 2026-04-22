using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenClawNet.Models.Abstractions;

namespace OpenClawNet.Models.FoundryLocal;

/// <summary>
/// MAF-compatible agent provider for Microsoft Foundry Local (on-device inference).
/// Bridges the existing <see cref="FoundryLocalModelClient"/> into an <see cref="IChatClient"/>
/// via <see cref="FoundryLocalChatClientBridge"/>.
/// </summary>
public sealed class FoundryLocalAgentProvider : IAgentProvider
{
    private readonly IOptions<FoundryLocalOptions> _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<FoundryLocalAgentProvider> _logger;

    public FoundryLocalAgentProvider(
        IOptions<FoundryLocalOptions> options,
        ILoggerFactory loggerFactory,
        ILogger<FoundryLocalAgentProvider> logger)
    {
        _options = options;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    public string ProviderName => "foundry-local";

    public IChatClient CreateChatClient(AgentProfile profile)
    {
        var model = _options.Value.Model ?? "phi-4";

        _logger.LogDebug("Creating Foundry Local IChatClient via bridge: model={Model}", model);

        var opts = Options.Create(new FoundryLocalOptions
        {
            AppName = _options.Value.AppName,
            Model = model,
            Temperature = profile.Temperature ?? _options.Value.Temperature,
            MaxTokens = profile.MaxTokens ?? _options.Value.MaxTokens,
        });

        var client = new FoundryLocalModelClient(opts, _loggerFactory.CreateLogger<FoundryLocalModelClient>());
        return new FoundryLocalChatClientBridge(client);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var opts = Options.Create(_options.Value);
            var client = new FoundryLocalModelClient(opts, _loggerFactory.CreateLogger<FoundryLocalModelClient>());
            return await client.IsAvailableAsync(ct);
        }
        catch
        {
            return false;
        }
    }
}
