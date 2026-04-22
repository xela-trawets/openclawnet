using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Builder.TraceExtensions;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;

namespace OpenClawNet.Services.Channels;

public sealed class ChannelAdapterErrorHandler : CloudAdapter
{
    public ChannelAdapterErrorHandler(
        BotFrameworkAuthentication auth,
        ILogger<IBotFrameworkHttpAdapter> logger)
        : base(auth, logger)
    {
        OnTurnError = async (turnContext, exception) =>
        {
            logger.LogError(exception, "Unhandled exception in Teams bot turn");
            await turnContext.TraceActivityAsync("ChannelAdapter - OnTurnError", exception.Message, cancellationToken: default);
            await turnContext.SendActivityAsync(MessageFactory.Text($"An error occurred: {exception.Message}"), default);
        };
    }
}
