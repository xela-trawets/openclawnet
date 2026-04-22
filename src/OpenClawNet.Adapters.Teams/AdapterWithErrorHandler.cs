using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Logging;

namespace OpenClawNet.Adapters.Teams;

/// <summary>
/// CloudAdapter with a global error handler that logs exceptions and notifies the user.
/// </summary>
public sealed class AdapterWithErrorHandler : CloudAdapter
{
    public AdapterWithErrorHandler(
        BotFrameworkAuthentication auth,
        ILogger<AdapterWithErrorHandler> logger)
        : base(auth, logger)
    {
        OnTurnError = async (turnContext, exception) =>
        {
            logger.LogError(exception, "Unhandled exception in bot turn");
            await turnContext.SendActivityAsync(
                $"⚠️ An error occurred: {exception.Message}");
        };
    }
}
