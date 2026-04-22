using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using OpenClawNet.Gateway.Hubs;

namespace OpenClawNet.IntegrationTests;

/// <summary>
/// End-to-end streaming tests for the tool-call pipeline.
/// Uses GatewayToolCallWebAppFactory which registers FakeToolCallingModelClient —
/// a fake that returns tool calls in its StreamAsync method.
///
/// These tests verify that when the model returns tool calls during streaming:
/// - Tool-related events (tool_start, tool_complete) are emitted
/// - The streamed response is NOT empty
/// - The stream completes properly
/// </summary>
public sealed class ChatHubToolCallStreamTests(GatewayToolCallWebAppFactory factory)
    : IClassFixture<GatewayToolCallWebAppFactory>, IAsyncLifetime
{
    private HubConnection? _connection;

    public async Task InitializeAsync()
    {
        _connection = new HubConnectionBuilder()
            .WithUrl($"{factory.Server.BaseAddress}hubs/chat", opts =>
            {
                opts.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

        await _connection.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }

    [Fact]
    public async Task StreamChat_WithToolCalls_EmitsToolStartEvent()
    {
        var sessionId = Guid.NewGuid();
        var events = new List<ChatHubMessage>();

        var stream = _connection!.StreamAsync<ChatHubMessage>(
            "StreamChat", sessionId, "List files in /src", (string?)null);

        await foreach (var msg in stream)
        {
            events.Add(msg);
            if (msg.Type is "complete" or "error") break;
        }

        events.Should().Contain(m => m.Type == "tool_start",
            "when the model returns tool calls, a tool_start event should be emitted");
    }

    [Fact]
    public async Task StreamChat_WithToolCalls_EmitsToolCompleteEvent()
    {
        var sessionId = Guid.NewGuid();
        var events = new List<ChatHubMessage>();

        var stream = _connection!.StreamAsync<ChatHubMessage>(
            "StreamChat", sessionId, "List files in /src", (string?)null);

        await foreach (var msg in stream)
        {
            events.Add(msg);
            if (msg.Type is "complete" or "error") break;
        }

        events.Should().Contain(m => m.Type == "tool_complete",
            "after tool execution, a tool_complete event should be emitted");
    }

    [Fact]
    public async Task StreamChat_WithToolCalls_ResponseIsNotEmpty()
    {
        var sessionId = Guid.NewGuid();
        var events = new List<ChatHubMessage>();

        var stream = _connection!.StreamAsync<ChatHubMessage>(
            "StreamChat", sessionId, "List files in /src", (string?)null);

        await foreach (var msg in stream)
        {
            events.Add(msg);
            if (msg.Type is "complete" or "error") break;
        }

        events.Should().NotBeEmpty("the hub should yield at least one event");
        events.Count.Should().BeGreaterThan(1,
            "tool-call scenarios should produce multiple events (tool_start, tool_complete, content, complete)");
    }

    [Fact]
    public async Task StreamChat_WithToolCalls_EndsWithCompleteOrError()
    {
        var sessionId = Guid.NewGuid();
        var events = new List<ChatHubMessage>();

        var stream = _connection!.StreamAsync<ChatHubMessage>(
            "StreamChat", sessionId, "List files in /src", (string?)null);

        await foreach (var msg in stream)
        {
            events.Add(msg);
            if (msg.Type is "complete" or "error") break;
        }

        var lastEvent = events.Last();
        lastEvent.Type.Should().BeOneOf("complete", "error",
            "the final streaming event must be 'complete' or 'error'");
    }

    [Fact]
    public async Task StreamChat_WithToolCalls_ToolStartBeforeToolComplete()
    {
        var sessionId = Guid.NewGuid();
        var events = new List<ChatHubMessage>();

        var stream = _connection!.StreamAsync<ChatHubMessage>(
            "StreamChat", sessionId, "List files in /src", (string?)null);

        await foreach (var msg in stream)
        {
            events.Add(msg);
            if (msg.Type is "complete" or "error") break;
        }

        var toolStartIndex = events.FindIndex(m => m.Type == "tool_start");
        var toolCompleteIndex = events.FindIndex(m => m.Type == "tool_complete");

        if (toolStartIndex >= 0 && toolCompleteIndex >= 0)
        {
            toolStartIndex.Should().BeLessThan(toolCompleteIndex,
                "tool_start must come before tool_complete in the event stream");
        }
    }
}
