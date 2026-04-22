using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenClawNet.Models.Abstractions;
using OpenClawChatMessage = OpenClawNet.Models.Abstractions.ChatMessage;
using AIAgentChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace OpenClawNet.Agent;

/// <summary>
/// Microsoft Agent Framework host adapter that delegates model execution to the solution-owned IModelClient.
/// Keeps Agent Framework orchestration internal while preserving OpenClawNet abstractions.
/// </summary>
internal sealed class ModelClientAgentHost : AIAgent
{
    private readonly IModelClient _modelClient;
    private readonly ILogger<ModelClientAgentHost> _logger;

    public ModelClientAgentHost(IModelClient modelClient, ILogger<ModelClientAgentHost> logger)
    {
        _modelClient = modelClient;
        _logger = logger;
    }

    protected override string? IdCore => "openclawnet-model-client-host";

    protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult<AgentSession>(null!);

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session,
        JsonSerializerOptions? jsonSerializerOptions,
        CancellationToken cancellationToken)
    {
        var serialized = JsonSerializer.SerializeToElement(new { });
        return new(serialized);
    }

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState,
        JsonSerializerOptions? jsonSerializerOptions,
        CancellationToken cancellationToken)
        => ValueTask.FromResult<AgentSession>(null!);

    protected override async Task<Microsoft.Agents.AI.AgentResponse> RunCoreAsync(
        IEnumerable<AIAgentChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        CancellationToken cancellationToken)
    {
        var chatRequest = BuildChatRequest(messages, options);
        var chatResponse = await _modelClient.CompleteAsync(chatRequest, cancellationToken);

        var contents = new List<AIContent>();
        if (!string.IsNullOrWhiteSpace(chatResponse.Content))
        {
            contents.Add(new TextContent(chatResponse.Content));
        }

        if (chatResponse.ToolCalls is { Count: > 0 })
        {
            foreach (var toolCall in chatResponse.ToolCalls)
            {
                contents.Add(new FunctionCallContent(toolCall.Id, toolCall.Name, ParseArguments(toolCall.Arguments)));
            }
        }

        var assistantMessage = new AIAgentChatMessage(ChatRole.Assistant, contents);
        var agentResponse = new Microsoft.Agents.AI.AgentResponse(assistantMessage)
        {
            AgentId = Id
        };

        agentResponse.AdditionalProperties ??= new();
        agentResponse.AdditionalProperties["openclaw.chatResponse"] = chatResponse;

        return agentResponse;
    }

    protected override async IAsyncEnumerable<Microsoft.Agents.AI.AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<AIAgentChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var chatRequest = BuildChatRequest(messages, options);

        await foreach (var chunk in _modelClient.StreamAsync(chatRequest, cancellationToken))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                yield return new AgentResponseUpdate(ChatRole.Assistant, chunk.Content)
                {
                    AgentId = Id
                };
            }
        }
    }

    private ChatRequest BuildChatRequest(IEnumerable<AIAgentChatMessage> messages, AgentRunOptions? options)
    {
        var additional = options?.AdditionalProperties;

        // Null/empty model means "use provider default" — OllamaModelClient falls back to OllamaOptions.Model
        var model = additional is not null
            && additional.TryGetValue("openclaw.model", out var modelValue)
            && modelValue is string { Length: > 0 } m
            ? m : null;

        IReadOnlyList<ToolDefinition>? tools = null;
        if (additional is not null && additional.TryGetValue("openclaw.tools", out var toolsValue) && toolsValue is IReadOnlyList<ToolDefinition> typedTools)
        {
            tools = typedTools;
        }

        var openClawMessages = messages.Select(ToOpenClawMessage).ToList();

        _logger.LogDebug("Running Agent Framework host against model {Model} with {MessageCount} messages", model, openClawMessages.Count);

        return new ChatRequest
        {
            Model = model,
            Messages = openClawMessages,
            Tools = tools
        };
    }

    private static OpenClawChatMessage ToOpenClawMessage(AIAgentChatMessage message)
    {
        var roleValue = message.Role.Value;
        var role = roleValue switch
        {
            var v when v == ChatRole.System.Value => ChatMessageRole.System,
            var v when v == ChatRole.User.Value => ChatMessageRole.User,
            var v when v == ChatRole.Assistant.Value => ChatMessageRole.Assistant,
            var v when v == ChatRole.Tool.Value => ChatMessageRole.Tool,
            _ => ChatMessageRole.User
        };

        var text = message.Text ?? string.Empty;
        var toolCalls = message.Contents
            .OfType<FunctionCallContent>()
            .Select(c => new OpenClawNet.Models.Abstractions.ToolCall
            {
                Id = c.CallId ?? Guid.NewGuid().ToString("N"),
                Name = c.Name,
                Arguments = JsonSerializer.Serialize(c.Arguments)
            })
            .ToList();

        return new OpenClawChatMessage
        {
            Role = role,
            Content = text,
            ToolCalls = toolCalls.Count > 0 ? toolCalls.AsReadOnly() : null
        };
    }

    private static IDictionary<string, object?> ParseArguments(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return new Dictionary<string, object?>();

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(arguments);
            return parsed ?? new Dictionary<string, object?>();
        }
        catch
        {
            return new Dictionary<string, object?>
            {
                ["raw"] = arguments
            };
        }
    }
}
