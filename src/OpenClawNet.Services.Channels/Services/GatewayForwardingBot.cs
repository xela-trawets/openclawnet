using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;

namespace OpenClawNet.Services.Channels;

/// <summary>
/// Bot that forwards Teams messages to the OpenClawNet Gateway REST API.
/// Decouples Teams Bot Framework from the agent runtime process.
/// </summary>
public sealed class GatewayForwardingBot : TeamsActivityHandler
{
    private readonly HttpClient _gateway;
    private readonly ILogger<GatewayForwardingBot> _logger;
    private static readonly ConcurrentDictionary<string, Guid> _sessionMap = new();

    public GatewayForwardingBot(HttpClient gateway, ILogger<GatewayForwardingBot> logger)
    {
        _gateway = gateway;
        _logger = logger;
    }

    protected override async Task OnMessageActivityAsync(
        ITurnContext<IMessageActivity> turnContext,
        CancellationToken cancellationToken)
    {
        var conversationId = turnContext.Activity.Conversation.Id;
        var sessionId = _sessionMap.GetOrAdd(conversationId, _ => Guid.NewGuid());
        var userMessage = turnContext.Activity.Text?.Trim();

        if (string.IsNullOrEmpty(userMessage))
        {
            await turnContext.SendActivityAsync(MessageFactory.Text("Please send a text message."), cancellationToken);
            return;
        }

        _logger.LogInformation("Teams message for session {SessionId}: {Message}", sessionId, userMessage);

        try
        {
            var response = await _gateway.PostAsJsonAsync("/api/chat/",
                new { sessionId, message = userMessage }, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<GatewayChatResult>(cancellationToken: cancellationToken);
            await turnContext.SendActivityAsync(
                MessageFactory.Text(result?.Content ?? "I couldn't process your request."), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to forward message to Gateway");
            await turnContext.SendActivityAsync(
                MessageFactory.Text("Sorry, I'm having trouble connecting to the agent. Please try again."), cancellationToken);
        }
    }

    protected override async Task OnMembersAddedAsync(
        IList<ChannelAccount> membersAdded,
        ITurnContext<IConversationUpdateActivity> turnContext,
        CancellationToken cancellationToken)
    {
        foreach (var member in membersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
                await turnContext.SendActivityAsync(MessageFactory.Text("👋 Hello! I'm OpenClawNet, your AI agent. How can I help?"), cancellationToken);
        }
    }

    private sealed record GatewayChatResult(string Content, int ToolCallCount, int TotalTokens);
}
