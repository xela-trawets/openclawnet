using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenClawNet.Models.Abstractions;
using OpenClawNet.Models.Ollama;

namespace OpenClawNet.UnitTests.Models;

/// <summary>
/// Live streaming tool-call tests against a real Ollama instance.
/// Requires Ollama running at localhost:11434 with gemma4:e2b installed.
/// Run with: dotnet test --filter "Category=Live"
/// Skipped automatically when Ollama is unreachable.
/// </summary>
[Trait("Category", "Live")]
public sealed class OllamaStreamingToolCallLiveTests
{
    private const string OllamaEndpoint = "http://localhost:11434";
    private const string Model = "gemma4:e2b";

    [SkippableFact]
    public async Task StreamAsync_WithTools_YieldsToolCallChunk()
    {
        var client = BuildClient();
        Skip.IfNot(await client.IsAvailableAsync(), "Ollama is not running at localhost:11434");

        var request = new ChatRequest
        {
            Model = Model,
            Messages =
            [
                new ChatMessage
                {
                    Role = ChatMessageRole.System,
                    Content = "You are a helpful assistant. When the user asks to list files, always use the list_files tool."
                },
                new ChatMessage
                {
                    Role = ChatMessageRole.User,
                    Content = "List the files in /src"
                }
            ],
            Tools =
            [
                new ToolDefinition
                {
                    Name = "list_files",
                    Description = "Lists files and directories at the given path",
                    Parameters = JsonDocument.Parse("""
                        {
                            "type": "object",
                            "properties": {
                                "path": {
                                    "type": "string",
                                    "description": "The directory path to list"
                                }
                            },
                            "required": ["path"]
                        }
                        """)
                }
            ]
        };

        var chunks = new List<ChatResponseChunk>();
        await foreach (var chunk in client.StreamAsync(request))
        {
            chunks.Add(chunk);
        }

        chunks.Should().NotBeEmpty("streaming should yield at least one chunk");

        var toolCallChunks = chunks.Where(c => c.ToolCalls is { Count: > 0 }).ToList();
        toolCallChunks.Should().NotBeEmpty(
            "when tools are provided and the prompt asks to use them, the model should return at least one tool call");

        var firstToolCall = toolCallChunks[0].ToolCalls![0];
        firstToolCall.Name.Should().Be("list_files");
        firstToolCall.Id.Should().NotBeNullOrWhiteSpace();
        firstToolCall.Arguments.Should().Contain("path");
    }

    private static OllamaModelClient BuildClient()
    {
        var options = Options.Create(new OllamaOptions
        {
            Endpoint = OllamaEndpoint,
            Model = Model,
            Temperature = 0.0,
            MaxTokens = 512
        });
        var httpClient = new HttpClient { BaseAddress = new Uri(OllamaEndpoint) };
        return new OllamaModelClient(httpClient, options, NullLogger<OllamaModelClient>.Instance);
    }
}
