using System.ClientModel;
using System.Runtime.CompilerServices;
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using OpenClawNet.Models.Abstractions;

namespace OpenClawNet.Models.AzureOpenAI;

public sealed class AzureOpenAIModelClient : IModelClient
{
    private readonly AzureOpenAIOptions _options;
    private readonly ILogger<AzureOpenAIModelClient> _logger;
    private readonly ChatClient? _chatClient;

    public AzureOpenAIModelClient(IOptions<AzureOpenAIOptions> options, ILogger<AzureOpenAIModelClient> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrEmpty(_options.Endpoint))
            throw new InvalidOperationException(
                "Azure OpenAI endpoint not configured. Set Model:Endpoint via User Secrets, environment variables, or the Settings UI.");

        AzureOpenAIClient azureClient;

        var clientOptions = new AzureOpenAIClientOptions();
        // Prevent indefinite hangs: timeout the initial HTTP response after 60 seconds.
        // Streaming tokens flow over the open connection — this only guards the handshake.
        clientOptions.NetworkTimeout = TimeSpan.FromSeconds(60);

        if (_options.AuthMode.Equals("integrated", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Azure OpenAI: using integrated authentication (DefaultAzureCredential)");
            azureClient = new AzureOpenAIClient(new Uri(_options.Endpoint), new DefaultAzureCredential(), clientOptions);
        }
        else
        {
            if (string.IsNullOrEmpty(_options.ApiKey))
                throw new InvalidOperationException(
                    "Azure OpenAI API key not configured. Set it via User Secrets or the Settings UI.");

            _logger.LogInformation("Azure OpenAI: using API key authentication");
            azureClient = new AzureOpenAIClient(new Uri(_options.Endpoint), new AzureKeyCredential(_options.ApiKey), clientOptions);
        }

        _chatClient = azureClient.GetChatClient(_options.DeploymentName);
    }

    public string ProviderName => "azure-openai";

    public async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        if (_chatClient is null)
            throw new InvalidOperationException(
                "Azure OpenAI is not configured. Set Model:Endpoint and Model:ApiKey (or use AuthMode=integrated).");

        var deployment = string.IsNullOrEmpty(request.Model) ? _options.DeploymentName : request.Model;
        var messages = MapMessages(request.Messages);
        var completionOptions = new ChatCompletionOptions();
        if (request.Temperature.HasValue)
            completionOptions.Temperature = (float)request.Temperature.Value;
        if (request.MaxTokens.HasValue)
            completionOptions.MaxOutputTokenCount = request.MaxTokens.Value;
        MapTools(request.Tools, completionOptions);

        _logger.LogDebug("Sending chat to Azure OpenAI: deployment={Deployment}, auth={AuthMode}",
            deployment, _options.AuthMode);

        var result = await _chatClient.CompleteChatAsync(messages, completionOptions, cancellationToken);
        var completion = result.Value;

        List<Abstractions.ToolCall>? toolCalls = null;
        if (completion.ToolCalls is { Count: > 0 })
        {
            toolCalls = completion.ToolCalls
                .Select(tc => new Abstractions.ToolCall
                {
                    Id = tc.Id,
                    Name = tc.FunctionName,
                    Arguments = tc.FunctionArguments?.ToString() ?? "{}"
                })
                .ToList();
        }

        return new ChatResponse
        {
            Content = completion.Content.FirstOrDefault()?.Text ?? string.Empty,
            Role = Abstractions.ChatMessageRole.Assistant,
            Model = deployment,
            ToolCalls = toolCalls,
            Usage = new UsageInfo
            {
                PromptTokens = completion.Usage.InputTokenCount,
                CompletionTokens = completion.Usage.OutputTokenCount,
                TotalTokens = completion.Usage.TotalTokenCount
            },
            FinishReason = completion.FinishReason.ToString()
        };
    }

    public async IAsyncEnumerable<ChatResponseChunk> StreamAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_chatClient is null)
            throw new InvalidOperationException(
                "Azure OpenAI is not configured. Set Model:Endpoint and Model:ApiKey (or use AuthMode=integrated).");

        var deployment = string.IsNullOrEmpty(request.Model) ? _options.DeploymentName : request.Model;
        var messages = MapMessages(request.Messages);
        var completionOptions = new ChatCompletionOptions();
        if (request.Temperature.HasValue)
            completionOptions.Temperature = (float)request.Temperature.Value;
        if (request.MaxTokens.HasValue)
            completionOptions.MaxOutputTokenCount = request.MaxTokens.Value;
        MapTools(request.Tools, completionOptions);

        _logger.LogDebug("Starting streaming chat with Azure OpenAI: deployment={Deployment}", deployment);

        AsyncCollectionResult<StreamingChatCompletionUpdate> stream =
            _chatClient.CompleteChatStreamingAsync(messages, completionOptions, cancellationToken);

        // Azure OpenAI sends tool calls incrementally: first chunk has name+id,
        // subsequent chunks have argument fragments. Accumulate by index.
        var accumulatedToolCalls = new Dictionary<int, (string? Id, string? Name, string Arguments)>();

        await foreach (var update in stream)
        {
            var content = update.ContentUpdate.FirstOrDefault()?.Text;

            if (update.ToolCallUpdates is { Count: > 0 })
            {
                foreach (var tc in update.ToolCallUpdates)
                {
                    if (!accumulatedToolCalls.TryGetValue(tc.Index, out var existing))
                        existing = (null, null, "");

                    accumulatedToolCalls[tc.Index] = (
                        tc.ToolCallId ?? existing.Id,
                        tc.FunctionName ?? existing.Name,
                        existing.Arguments + (tc.FunctionArgumentsUpdate?.ToString() ?? "")
                    );
                }
            }

            // Emit accumulated tool calls when the stream signals completion
            List<Abstractions.ToolCall>? toolCalls = null;
            if (update.FinishReason is not null && accumulatedToolCalls.Count > 0)
            {
                toolCalls = accumulatedToolCalls.Values
                    .Where(tc => !string.IsNullOrEmpty(tc.Name))
                    .Select(tc => new Abstractions.ToolCall
                    {
                        Id = tc.Id ?? $"call_{Guid.NewGuid():N}",
                        Name = tc.Name!,
                        Arguments = tc.Arguments
                    })
                    .ToList();
                accumulatedToolCalls.Clear();
            }

            yield return new ChatResponseChunk
            {
                Content = content,
                ToolCalls = toolCalls is { Count: > 0 } ? toolCalls.AsReadOnly() : null,
                FinishReason = update.FinishReason?.ToString()
            };
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (_chatClient is null) return false;

        try
        {
            await _chatClient.CompleteChatAsync(
                [new UserChatMessage("ping")],
                new ChatCompletionOptions(),
                cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void MapTools(IReadOnlyList<ToolDefinition>? tools, ChatCompletionOptions options)
    {
        if (tools is not { Count: > 0 }) return;

        foreach (var tool in tools)
        {
            var parameters = tool.Parameters is not null
                ? BinaryData.FromString(tool.Parameters.RootElement.GetRawText())
                : null;
            options.Tools.Add(ChatTool.CreateFunctionTool(tool.Name, tool.Description ?? "", parameters));
        }
    }

    private static List<OpenAI.Chat.ChatMessage> MapMessages(IReadOnlyList<Abstractions.ChatMessage> messages)
    {
        var result = new List<OpenAI.Chat.ChatMessage>();

        foreach (var msg in messages)
        {
            OpenAI.Chat.ChatMessage mapped = msg.Role switch
            {
                Abstractions.ChatMessageRole.System    => new SystemChatMessage(msg.Content),
                Abstractions.ChatMessageRole.User      => new UserChatMessage(msg.Content),
                Abstractions.ChatMessageRole.Assistant => MapAssistantMessage(msg),
                Abstractions.ChatMessageRole.Tool      => new ToolChatMessage(msg.ToolCallId ?? "unknown", msg.Content),
                _                                      => new UserChatMessage(msg.Content)
            };
            result.Add(mapped);
        }

        return result;
    }

    /// <summary>
    /// Maps an OpenClaw assistant message to the Azure OpenAI SDK format.
    /// Crucially, tool calls must be included — Azure OpenAI rejects tool-result
    /// messages that aren't preceded by an assistant message with matching tool calls.
    /// </summary>
    private static AssistantChatMessage MapAssistantMessage(Abstractions.ChatMessage msg)
    {
        var assistantMsg = new AssistantChatMessage(msg.Content);

        if (msg.ToolCalls is { Count: > 0 })
        {
            foreach (var tc in msg.ToolCalls)
            {
                assistantMsg.ToolCalls.Add(
                    ChatToolCall.CreateFunctionToolCall(
                        tc.Id,
                        tc.Name,
                        BinaryData.FromString(tc.Arguments ?? "{}")));
            }
        }

        return assistantMsg;
    }
}
