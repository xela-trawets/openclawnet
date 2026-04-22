using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using OpenClawNet.Gateway.Endpoints;

namespace OpenClawNet.IntegrationTests;

public sealed class SessionsApiTests(GatewayWebAppFactory factory) : IClassFixture<GatewayWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ListSessions_ReturnsOk_WhenEmpty()
    {
        var response = await _client.GetAsync("/api/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var sessions = JsonSerializer.Deserialize<SessionDto[]>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        sessions.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateSession_ReturnsCreated()
    {
        var response = await _client.PostAsJsonAsync("/api/sessions", new { title = "Test Session" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync();
        var session = JsonSerializer.Deserialize<SessionDto>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        session.Should().NotBeNull();
        session!.Title.Should().Be("Test Session");
        session.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateSession_NoTitle_ReturnsCreated_WithDefaultTitle()
    {
        var response = await _client.PostAsJsonAsync("/api/sessions", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync();
        var session = JsonSerializer.Deserialize<SessionDto>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        session.Should().NotBeNull();
        session!.Title.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetSession_ReturnsSession_WhenExists()
    {
        // Create session first
        var created = await CreateSessionAndGetId("Get Test");

        var response = await _client.GetAsync($"/api/sessions/{created}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var session = JsonSerializer.Deserialize<SessionDetailDto>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        session!.Id.Should().Be(created);
        session.Title.Should().Be("Get Test");
    }

    [Fact]
    public async Task GetSession_Returns404_WhenNotFound()
    {
        var response = await _client.GetAsync($"/api/sessions/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteSession_ReturnsNoContent()
    {
        var id = await CreateSessionAndGetId("Delete Me");

        var response = await _client.DeleteAsync($"/api/sessions/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteSession_MakesSessionUnreachable()
    {
        var id = await CreateSessionAndGetId("Gone");
        await _client.DeleteAsync($"/api/sessions/{id}");

        var response = await _client.GetAsync($"/api/sessions/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateTitle_ChangesSessionTitle()
    {
        var id = await CreateSessionAndGetId("Old Title");

        var patch = await _client.PatchAsJsonAsync($"/api/sessions/{id}/title", new { title = "New Title" });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _client.GetAsync($"/api/sessions/{id}");
        var body = await response.Content.ReadAsStringAsync();
        var session = JsonSerializer.Deserialize<SessionDetailDto>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        session!.Title.Should().Be("New Title");
    }

    [Fact]
    public async Task ListSessions_IncludesCreatedSessions()
    {
        await CreateSessionAndGetId("Listed Session A");
        await CreateSessionAndGetId("Listed Session B");

        var response = await _client.GetAsync("/api/sessions");
        var body = await response.Content.ReadAsStringAsync();
        var sessions = JsonSerializer.Deserialize<SessionDto[]>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        sessions.Should().Contain(s => s.Title == "Listed Session A");
        sessions.Should().Contain(s => s.Title == "Listed Session B");
    }

    private async Task<Guid> CreateSessionAndGetId(string title)
    {
        var response = await _client.PostAsJsonAsync("/api/sessions", new { title });
        var body = await response.Content.ReadAsStringAsync();
        var session = JsonSerializer.Deserialize<SessionDto>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return session!.Id;
    }
}
