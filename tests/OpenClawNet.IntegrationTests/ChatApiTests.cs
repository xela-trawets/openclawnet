using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace OpenClawNet.IntegrationTests;

public sealed class ChatApiTests(GatewayWebAppFactory factory) : IClassFixture<GatewayWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task PostChat_ReturnsOk_WithContent()
    {
        var sessionId = await CreateSession();

        var response = await _client.PostAsJsonAsync("/api/chat", new
        {
            sessionId,
            message = "Hello!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var result = JsonDocument.Parse(body).RootElement;
        result.GetProperty("content").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PostChat_ReturnsContentFromFakeModel()
    {
        var sessionId = await CreateSession();

        var response = await _client.PostAsJsonAsync("/api/chat", new
        {
            sessionId,
            message = "Ping"
        });

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonDocument.Parse(body).RootElement;
        result.GetProperty("content").GetString().Should().Contain("fake model");
    }

    [Fact]
    public async Task PostChat_AutoCreatesSession_WhenSessionIdUnknown()
    {
        // Client-generated session ID (no prior POST /api/sessions call)
        var unknownSessionId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync("/api/chat", new
        {
            sessionId = unknownSessionId,
            message = "Hi"
        });

        // Should succeed even though session wasn't pre-created
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostChat_StoresMessagesInHistory()
    {
        var sessionId = await CreateSession();

        await _client.PostAsJsonAsync("/api/chat", new { sessionId, message = "First" });
        await _client.PostAsJsonAsync("/api/chat", new { sessionId, message = "Second" });

        var msgResponse = await _client.GetAsync($"/api/sessions/{sessionId}/messages");
        var body = await msgResponse.Content.ReadAsStringAsync();
        var messages = JsonDocument.Parse(body).RootElement;

        messages.GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
    }

    private async Task<Guid> CreateSession()
    {
        var response = await _client.PostAsJsonAsync("/api/sessions", new { title = "Chat Test" });
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body).RootElement;
        return json.GetProperty("id").GetGuid();
    }
}
