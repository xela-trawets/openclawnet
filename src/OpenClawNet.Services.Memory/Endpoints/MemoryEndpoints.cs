using Microsoft.Extensions.Options;
using OpenClawNet.Models.Abstractions;
using OpenClawNet.Models.Ollama;

namespace OpenClawNet.Services.Memory.Endpoints;

public static class MemoryEndpoints
{
    public static void MapMemoryEndpoints(this WebApplication app)
    {
        app.MapPost("/api/memory/summarize", async (SummarizeRequest request, IModelClient modelClient, IOptions<OllamaOptions> ollamaOptions, ILogger<SummarizeRequest> logger) =>
        {
            if (request.Messages is null || request.Messages.Count == 0)
                return Results.BadRequest(new SummarizeResponse { Success = false, Error = "No messages provided" });

            try
            {
                var conversationText = string.Join("\n", request.Messages.Select(m => $"{m.Role}: {m.Content}"));
                var summaryRequest = new ChatRequest
                {
                    Model = request.Model ?? ollamaOptions.Value.Model,
                    Messages =
                    [
                        new ChatMessage
                        {
                            Role = ChatMessageRole.System,
                            Content = "You are a conversation summarizer. Summarize the following conversation concisely, preserving key facts, decisions, and context. Keep the summary under 500 words."
                        },
                        new ChatMessage
                        {
                            Role = ChatMessageRole.User,
                            Content = $"Please summarize this conversation:\n\n{conversationText}"
                        }
                    ]
                };

                var response = await modelClient.CompleteAsync(summaryRequest);
                logger.LogInformation("Summarized {MessageCount} messages for session {SessionId}", request.Messages.Count, request.SessionId);
                return Results.Ok(new SummarizeResponse { Success = true, Summary = response.Content, MessageCount = request.Messages.Count });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Summarization failed for session {SessionId}", request.SessionId);
                return Results.Ok(new SummarizeResponse { Success = false, Error = ex.Message });
            }
        })
        .WithTags("Memory")
        .WithName("SummarizeConversation");
    }
}

public sealed record SummarizeRequest
{
    public Guid SessionId { get; init; }
    public List<MessageDto>? Messages { get; init; }
    public string? Model { get; init; }
}

public sealed record MessageDto(string Role, string Content);
public sealed record SummarizeResponse
{
    public bool Success { get; init; }
    public string? Summary { get; init; }
    public string? Error { get; init; }
    public int MessageCount { get; init; }
}
