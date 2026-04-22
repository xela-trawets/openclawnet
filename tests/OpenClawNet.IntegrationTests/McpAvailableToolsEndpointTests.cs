using System.Net.Http.Json;
using FluentAssertions;

namespace OpenClawNet.IntegrationTests;

/// <summary>
/// PR-D: <c>/api/mcp/tools/available</c> backs the AgentProfiles tool picker. It
/// returns tools grouped by their owning server (storage-form names + descriptions).
/// </summary>
public sealed class McpAvailableToolsEndpointTests : IClassFixture<GatewayWebAppFactory>
{
    private readonly GatewayWebAppFactory _factory;
    public McpAvailableToolsEndpointTests(GatewayWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task AvailableTools_ReturnsBundledServerGroups()
    {
        var client = _factory.CreateClient();

        var groups = await client.GetFromJsonAsync<List<ToolGroup>>("/api/mcp/tools/available");

        groups.Should().NotBeNull();
        // Bundled in-process servers always appear in the merged catalog (PR-B/PR-C).
        groups!.Should().Contain(g => g.ServerName == "web");
        groups.Should().Contain(g => g.ServerName == "shell");

        // Storage-form names use dotted notation.
        var allNames = groups.SelectMany(g => g.Tools).Select(t => t.Name).ToList();
        allNames.Should().OnlyContain(n => n.Contains('.'));
    }

    private sealed record ToolGroup(string ServerId, string ServerName, bool IsLegacy, List<ToolEntry> Tools);
    private sealed record ToolEntry(string Name, string Description);
}
