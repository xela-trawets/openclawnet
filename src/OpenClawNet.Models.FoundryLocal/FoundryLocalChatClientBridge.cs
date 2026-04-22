using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using MEAIChatMessage = Microsoft.Extensions.AI.ChatMessage;
using MEAIChatResponse = Microsoft.Extensions.AI.ChatResponse;
using OCChatMessage = OpenClawNet.Models.Abstractions.ChatMessage;
using OCChatMessageRole = OpenClawNet.Models.Abstractions.ChatMessageRole;
using OCChatRequest = OpenClawNet.Models.Abstractions.ChatRequest;
using OCChatResponse = OpenClawNet.Models.Abstractions.ChatResponse;

namespace OpenClawNet.Models.FoundryLocal;

/// <summary>
/// Bridges the existing <see cref="FoundryLocalModelClient"/> to <see cref="IChatClient"/>
/// so the MAF provider infrastructure can use it without rewriting the Foundry Local SDK logic.
/// </summary>
internal sealed class FoundryLocalChatClientBridge : IChatClient
{
    private readonly FoundryLocalModelClient _client;

    public FoundryLocalChatClientBridge(FoundryLocalModelClient client) => _client = client;

    public async Task<MEAIChatResponse> GetResponseAsync(
        IEnumerable<MEAIChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = ToChatRequest(messages, options);
        var response = await _client.CompleteAsync(request, cancellationToken);
        return ToChatResponse(response);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<MEAIChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = ToChatRequest(messages, options);
        await foreach (var chunk in _client.StreamAsync(request, cancellationToken))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                yield return new ChatResponseUpdate
                {
                    Role = ChatRole.Assistant,
                    Contents = [new TextContent(chunk.Content)]
                };
            }
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }

    private static OCChatRequest ToChatRequest(
        IEnumerable<MEAIChatMessage> messages, ChatOptions? options)
    {
        var mapped = messages.Select(m => new OCChatMessage
        {
            Role = m.Role.Value switch
            {
                "system" => OCChatMessageRole.System,
                "user" => OCChatMessageRole.User,
                "assistant" => OCChatMessageRole.Assistant,
                "tool" => OCChatMessageRole.Tool,
                _ => OCChatMessageRole.User
            },
            Content = m.Text ?? string.Empty
        }).ToList();

        return new OCChatRequest
        {
            Messages = mapped,
            Temperature = options?.Temperature,
            MaxTokens = options?.MaxOutputTokens
        };
    }

    private static MEAIChatResponse ToChatResponse(OCChatResponse response)
    {
        var contents = new List<AIContent>();
        if (!string.IsNullOrEmpty(response.Content))
            contents.Add(new TextContent(response.Content));

        var msg = new MEAIChatMessage(ChatRole.Assistant, contents);
        var chatResponse = new MEAIChatResponse(msg);

        if (response.Usage is { } u)
        {
            chatResponse.Usage = new UsageDetails
            {
                InputTokenCount = u.PromptTokens,
                OutputTokenCount = u.CompletionTokens,
                TotalTokenCount = u.TotalTokens
            };
        }

        return chatResponse;
    }
}
