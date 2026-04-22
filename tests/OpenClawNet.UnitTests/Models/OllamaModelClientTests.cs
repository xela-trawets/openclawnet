using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenClawNet.Models.Abstractions;
using OpenClawNet.Models.Ollama;

namespace OpenClawNet.UnitTests.Models;

/// <summary>
/// Tests for OllamaModelClient request serialization and response deserialization.
/// Uses a fake HttpMessageHandler — no real Ollama instance needed.
/// </summary>
public sealed class OllamaModelClientTests
{
    [Fact]
    public async Task CompleteAsync_SendsModelAndMessages()
    {
        string? captured = null;
        var handler = FakeHandler(SimpleResponseJson(), captureRequest: body => captured = body);
        var client = BuildClient(handler);

        await client.CompleteAsync(new ChatRequest
        {
            Model = "gemma4:e2b",
            Messages = [new ChatMessage { Role = ChatMessageRole.User, Content = "Hi" }]
        });

        captured.Should().NotBeNull();
        var json = JsonDocument.Parse(captured!).RootElement;
        json.GetProperty("model").GetString().Should().Be("gemma4:e2b");
        json.GetProperty("messages")[0].GetProperty("role").GetString().Should().Be("user");
        json.GetProperty("messages")[0].GetProperty("content").GetString().Should().Be("Hi");
    }

    [Fact]
    public async Task CompleteAsync_SerializesTools_WhenProvided()
    {
        string? captured = null;
        var handler = FakeHandler(SimpleResponseJson(), captureRequest: body => captured = body);
        var client = BuildClient(handler);

        await client.CompleteAsync(new ChatRequest
        {
            Model = "test",
            Messages = [new ChatMessage { Role = ChatMessageRole.User, Content = "list files" }],
            Tools =
            [
                new ToolDefinition
                {
                    Name = "list_files",
                    Description = "Lists files in a directory",
                    Parameters = JsonDocument.Parse("""{"type":"object","properties":{"path":{"type":"string"}}}""")
                }
            ]
        });

        var json = JsonDocument.Parse(captured!).RootElement;
        json.TryGetProperty("tools", out var tools).Should().BeTrue();
        tools.GetArrayLength().Should().Be(1);
        tools[0].GetProperty("type").GetString().Should().Be("function");
        tools[0].GetProperty("function").GetProperty("name").GetString().Should().Be("list_files");
    }

    [Fact]
    public async Task CompleteAsync_SendsStreamFalse_ForNonStreaming()
    {
        string? captured = null;
        var handler = FakeHandler(SimpleResponseJson(), captureRequest: body => captured = body);
        var client = BuildClient(handler);

        await client.CompleteAsync(new ChatRequest
        {
            Model = "test",
            Messages = [new ChatMessage { Role = ChatMessageRole.User, Content = "Hi" }]
        });

        var json = JsonDocument.Parse(captured!).RootElement;
        json.GetProperty("stream").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task CompleteAsync_MapsToolCallsFromResponse()
    {
        var responseWithToolCall = """
        {
          "model": "gemma4:e2b",
          "message": {
            "role": "assistant",
            "content": "",
            "tool_calls": [
              { "function": { "name": "list_files", "arguments": { "path": "/src" } } }
            ]
          },
          "done": true,
          "prompt_eval_count": 10,
          "eval_count": 5
        }
        """;

        var handler = FakeHandler(responseWithToolCall);
        var client = BuildClient(handler);

        var response = await client.CompleteAsync(new ChatRequest
        {
            Model = "test",
            Messages = [new ChatMessage { Role = ChatMessageRole.User, Content = "List files" }]
        });

        response.ToolCalls.Should().HaveCount(1);
        response.ToolCalls![0].Name.Should().Be("list_files");
        response.ToolCalls![0].Arguments.Should().Contain("path");
    }

    [Fact]
    public async Task CompleteAsync_ReturnsUsageInfo()
    {
        var handler = FakeHandler("""
        {
          "model": "test",
          "message": { "role": "assistant", "content": "Hello!" },
          "done": true,
          "prompt_eval_count": 20,
          "eval_count": 8
        }
        """);
        var client = BuildClient(handler);

        var response = await client.CompleteAsync(new ChatRequest
        {
            Model = "test",
            Messages = [new ChatMessage { Role = ChatMessageRole.User, Content = "Hi" }]
        });

        response.Usage.Should().NotBeNull();
        response.Usage!.PromptTokens.Should().Be(20);
        response.Usage.CompletionTokens.Should().Be(8);
        response.Usage.TotalTokens.Should().Be(28);
    }

    [Fact]
    public async Task CompleteAsync_MapsChatMessageRoles_Correctly()
    {
        string? captured = null;
        var handler = FakeHandler(SimpleResponseJson(), captureRequest: body => captured = body);
        var client = BuildClient(handler);

        await client.CompleteAsync(new ChatRequest
        {
            Model = "test",
            Messages =
            [
                new ChatMessage { Role = ChatMessageRole.System, Content = "You are helpful" },
                new ChatMessage { Role = ChatMessageRole.User, Content = "Hi" },
                new ChatMessage { Role = ChatMessageRole.Assistant, Content = "Hello!" },
                new ChatMessage { Role = ChatMessageRole.Tool, Content = "result" },
            ]
        });

        var json = JsonDocument.Parse(captured!).RootElement;
        var msgs = json.GetProperty("messages");
        msgs[0].GetProperty("role").GetString().Should().Be("system");
        msgs[1].GetProperty("role").GetString().Should().Be("user");
        msgs[2].GetProperty("role").GetString().Should().Be("assistant");
        msgs[3].GetProperty("role").GetString().Should().Be("tool");
    }

    private static OllamaModelClient BuildClient(HttpMessageHandler handler)
    {
        var options = Options.Create(new OllamaOptions
        {
            Endpoint = "http://localhost:11434",
            Model = "llama3.2",
            Temperature = 0.7,
            MaxTokens = 2048
        });
        // BaseAddress is now configured by AddHttpClient in production; set it here for tests.
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(options.Value.Endpoint)
        };
        return new OllamaModelClient(httpClient, options, NullLogger<OllamaModelClient>.Instance);
    }

    private static HttpMessageHandler FakeHandler(string responseJson, Action<string>? captureRequest = null)
        => new FakeHttpHandler(responseJson, captureRequest);

    private static string SimpleResponseJson() =>
        """{"model":"test","message":{"role":"assistant","content":"Hello!"},"done":true,"prompt_eval_count":5,"eval_count":3}""";

    // ── Streaming tool-call tests ────────────────────────────────────────────

    [Fact]
    public async Task StreamAsync_ExtractsToolCalls_FromDoneFalseChunk()
    {
        // THE bug: Ollama sends tool_calls in a done=false chunk, not the done=true chunk.
        var ndjson =
            """
            {"model":"gemma4:e2b","message":{"role":"assistant","content":"","tool_calls":[{"function":{"name":"list_files","arguments":{"path":"/src"}}}]},"done":false}
            {"model":"gemma4:e2b","message":{"role":"assistant","content":""},"done":true,"prompt_eval_count":100,"eval_count":50}
            """;

        var handler = new StreamingFakeHttpHandler(ndjson);
        var client = BuildClient(handler);

        var chunks = new List<ChatResponseChunk>();
        await foreach (var chunk in client.StreamAsync(new ChatRequest
        {
            Model = "gemma4:e2b",
            Messages = [new ChatMessage { Role = ChatMessageRole.User, Content = "List files" }]
        }))
        {
            chunks.Add(chunk);
        }

        chunks.Should().HaveCount(2);

        // First chunk (done=false) must carry the tool call
        var toolChunk = chunks[0];
        toolChunk.ToolCalls.Should().NotBeNull();
        toolChunk.ToolCalls.Should().HaveCount(1);
        toolChunk.ToolCalls![0].Name.Should().Be("list_files");
        toolChunk.ToolCalls![0].Arguments.Should().Contain("path");
        toolChunk.FinishReason.Should().BeNull("done=false means no finish reason");

        // Second chunk (done=true) has no tool calls
        var finalChunk = chunks[1];
        finalChunk.FinishReason.Should().Be("stop");
    }

    [Fact]
    public async Task StreamAsync_PreservesToolCallIds_FromOllama()
    {
        var ndjson =
            """
            {"model":"gemma4:e2b","message":{"role":"assistant","content":"","tool_calls":[{"id":"call_abc123","function":{"name":"list_files","arguments":{"path":"/src"}}}]},"done":false}
            {"model":"gemma4:e2b","message":{"role":"assistant","content":""},"done":true,"prompt_eval_count":100,"eval_count":50}
            """;

        var handler = new StreamingFakeHttpHandler(ndjson);
        var client = BuildClient(handler);

        var chunks = new List<ChatResponseChunk>();
        await foreach (var chunk in client.StreamAsync(new ChatRequest
        {
            Model = "gemma4:e2b",
            Messages = [new ChatMessage { Role = ChatMessageRole.User, Content = "List files" }]
        }))
        {
            chunks.Add(chunk);
        }

        chunks[0].ToolCalls![0].Id.Should().Be("call_abc123");
    }

    [Fact]
    public async Task StreamAsync_GeneratesSyntheticId_WhenOllamaOmitsId()
    {
        var ndjson =
            """
            {"model":"gemma4:e2b","message":{"role":"assistant","content":"","tool_calls":[{"function":{"name":"list_files","arguments":{"path":"/src"}}}]},"done":false}
            {"model":"gemma4:e2b","message":{"role":"assistant","content":""},"done":true,"prompt_eval_count":100,"eval_count":50}
            """;

        var handler = new StreamingFakeHttpHandler(ndjson);
        var client = BuildClient(handler);

        var chunks = new List<ChatResponseChunk>();
        await foreach (var chunk in client.StreamAsync(new ChatRequest
        {
            Model = "gemma4:e2b",
            Messages = [new ChatMessage { Role = ChatMessageRole.User, Content = "List files" }]
        }))
        {
            chunks.Add(chunk);
        }

        // When Ollama omits the id, we generate "call_{index}_{name}"
        chunks[0].ToolCalls![0].Id.Should().StartWith("call_0_list_files");
    }

    [Fact]
    public async Task StreamAsync_ToolCallsInDoneTrueChunk_StillExtracted()
    {
        // Backward compat: some models may send tool_calls in the done=true chunk
        var ndjson =
            """
            {"model":"test","message":{"role":"assistant","content":"","tool_calls":[{"id":"call_99","function":{"name":"read_file","arguments":{"path":"readme.md"}}}]},"done":true,"prompt_eval_count":50,"eval_count":25}
            """;

        var handler = new StreamingFakeHttpHandler(ndjson);
        var client = BuildClient(handler);

        var chunks = new List<ChatResponseChunk>();
        await foreach (var chunk in client.StreamAsync(new ChatRequest
        {
            Model = "test",
            Messages = [new ChatMessage { Role = ChatMessageRole.User, Content = "Read readme" }]
        }))
        {
            chunks.Add(chunk);
        }

        chunks.Should().HaveCount(1);
        chunks[0].ToolCalls.Should().HaveCount(1);
        chunks[0].ToolCalls![0].Name.Should().Be("read_file");
        chunks[0].ToolCalls![0].Id.Should().Be("call_99");
        chunks[0].FinishReason.Should().Be("stop");
    }

    [Fact]
    public async Task StreamAsync_ToolCallsThenContent_BothPreserved()
    {
        // Stream: tool calls chunk, then a content chunk, then done
        var ndjson =
            """
            {"model":"gemma4:e2b","message":{"role":"assistant","content":"","tool_calls":[{"id":"call_1","function":{"name":"list_files","arguments":{"path":"/"}}}]},"done":false}
            {"model":"gemma4:e2b","message":{"role":"assistant","content":"Here are the files."},"done":false}
            {"model":"gemma4:e2b","message":{"role":"assistant","content":""},"done":true,"prompt_eval_count":80,"eval_count":40}
            """;

        var handler = new StreamingFakeHttpHandler(ndjson);
        var client = BuildClient(handler);

        var chunks = new List<ChatResponseChunk>();
        await foreach (var chunk in client.StreamAsync(new ChatRequest
        {
            Model = "gemma4:e2b",
            Messages = [new ChatMessage { Role = ChatMessageRole.User, Content = "List files" }]
        }))
        {
            chunks.Add(chunk);
        }

        chunks.Should().HaveCount(3);
        chunks[0].ToolCalls.Should().HaveCount(1);
        chunks[1].Content.Should().Be("Here are the files.");
        chunks[1].ToolCalls.Should().BeNull();
        chunks[2].FinishReason.Should().Be("stop");
    }

    [Fact]
    public async Task StreamAsync_MultipleToolCalls_InSingleChunk()
    {
        var ndjson =
            """
            {"model":"gemma4:e2b","message":{"role":"assistant","content":"","tool_calls":[{"id":"call_a","function":{"name":"list_files","arguments":{"path":"/src"}}},{"id":"call_b","function":{"name":"read_file","arguments":{"path":"/readme.md"}}}]},"done":false}
            {"model":"gemma4:e2b","message":{"role":"assistant","content":""},"done":true,"prompt_eval_count":100,"eval_count":50}
            """;

        var handler = new StreamingFakeHttpHandler(ndjson);
        var client = BuildClient(handler);

        var chunks = new List<ChatResponseChunk>();
        await foreach (var chunk in client.StreamAsync(new ChatRequest
        {
            Model = "gemma4:e2b",
            Messages = [new ChatMessage { Role = ChatMessageRole.User, Content = "List and read files" }]
        }))
        {
            chunks.Add(chunk);
        }

        chunks[0].ToolCalls.Should().HaveCount(2);
        chunks[0].ToolCalls![0].Name.Should().Be("list_files");
        chunks[0].ToolCalls![0].Id.Should().Be("call_a");
        chunks[0].ToolCalls![1].Name.Should().Be("read_file");
        chunks[0].ToolCalls![1].Id.Should().Be("call_b");
    }

    [Fact]
    public async Task StreamAsync_SendsStreamTrue_InRequest()
    {
        string? captured = null;
        var ndjson =
            """
            {"model":"test","message":{"role":"assistant","content":"Hi"},"done":true,"prompt_eval_count":5,"eval_count":3}
            """;

        var handler = new StreamingFakeHttpHandler(ndjson, captureRequest: body => captured = body);
        var client = BuildClient(handler);

        await foreach (var _ in client.StreamAsync(new ChatRequest
        {
            Model = "test",
            Messages = [new ChatMessage { Role = ChatMessageRole.User, Content = "Hi" }]
        }))
        { }

        captured.Should().NotBeNull();
        var json = JsonDocument.Parse(captured!).RootElement;
        json.GetProperty("stream").GetBoolean().Should().BeTrue();
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private sealed class FakeHttpHandler(string responseJson, Action<string>? onRequest = null) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null && onRequest is not null)
            {
                var body = await request.Content.ReadAsStringAsync(cancellationToken);
                onRequest(body);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class StreamingFakeHttpHandler(string ndJsonResponse, Action<string>? captureRequest = null) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null && captureRequest is not null)
            {
                var body = await request.Content.ReadAsStringAsync(cancellationToken);
                captureRequest(body);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ndJsonResponse, System.Text.Encoding.UTF8, "application/x-ndjson")
            };
        }
    }
}
