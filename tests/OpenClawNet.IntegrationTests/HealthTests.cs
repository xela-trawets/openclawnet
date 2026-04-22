using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace OpenClawNet.IntegrationTests;

public sealed class HealthTests(GatewayWebAppFactory factory) : IClassFixture<GatewayWebAppFactory>
{
    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_ReturnsHealthyStatus()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body).RootElement;

        json.GetProperty("status").GetString().Should().Be("healthy");
    }

    [Fact]
    public async Task Version_ReturnsOk()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/version");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Version_ReturnsExpectedFields()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/version");
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body).RootElement;

        json.GetProperty("name").GetString().Should().Be("OpenClawNet");
        json.GetProperty("version").GetString().Should().NotBeNullOrEmpty();
    }
}
