using System.Collections.Concurrent;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using OpenClawNet.Agent;

namespace OpenClawNet.Adapters.Teams;

/// <summary>
/// Bot activity handler. Routes incoming Teams messages to the OpenClawNet agent orchestrator
/// and sends the agent's response back to the Teams conversation.
/// </summary>
public sealed class OpenClawNetBot : TeamsActivityHandler
{
    private readonly IAgentOrchestrator _orchestrator;
    private readonly ILogger<OpenClawNetBot> _logger;

    // Maps Teams conversation ID → OpenClawNet session ID so each Teams thread is a persistent session.
    private static readonly ConcurrentDictionary<string, Guid> _sessionMap = new();

    public OpenClawNetBot(IAgentOrchestrator orchestrator, ILogger<OpenClawNetBot> logger)
    {
        _orchestrator = orchestrator;
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
            await turnContext.SendActivityAsync(
                MessageFactory.Text("Please send a text message."),
                cancellationToken);
            return;
        }

        _logger.LogInformation(
            "Teams message for session {SessionId} (conversation {ConvId}): {Message}",
            sessionId, conversationId, userMessage);

        var request = new AgentRequest
        {
            SessionId = sessionId,
            UserMessage = userMessage
        };

        var response = await _orchestrator.ProcessAsync(request, cancellationToken);

        await turnContext.SendActivityAsync(
            MessageFactory.Text(response.Content),
            cancellationToken);
    }

    protected override async Task OnMembersAddedAsync(
        IList<ChannelAccount> membersAdded,
        ITurnContext<IConversationUpdateActivity> turnContext,
        CancellationToken cancellationToken)
    {
        foreach (var member in membersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text("👋 Hello! I'm OpenClawNet, your AI agent. How can I help?"),
                    cancellationToken);
            }
        }
    }
}
