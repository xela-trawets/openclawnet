using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenClawNet.Mcp.Abstractions;
using OpenClawNet.Mcp.Core;

namespace OpenClawNet.UnitTests.Mcp;

public class McpToolProviderTests
{
    [Fact]
    public async Task GetAllToolsAsync_EmptyCatalog_ReturnsEmpty()
    {
        var provider = BuildProvider(servers: Array.Empty<McpServerDefinition>(), overrides: Array.Empty<McpToolOverride>());

        var tools = await provider.GetAllToolsAsync();

        tools.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllToolsAsync_DisabledServer_ReturnsEmpty()
    {
        var server = new McpServerDefinition
        {
            Id = Guid.NewGuid(),
            Name = "echo",
            Transport = McpTransport.InProcess,
            Enabled = false,
        };

        var provider = BuildProvider(servers: new[] { server }, overrides: Array.Empty<McpToolOverride>());

        var tools = await provider.GetAllToolsAsync();

        tools.Should().BeEmpty();
    }

    [Fact]
    public async Task GetToolsForServerAsync_UnknownId_ReturnsEmpty()
    {
        var provider = BuildProvider(servers: Array.Empty<McpServerDefinition>(), overrides: Array.Empty<McpToolOverride>());

        var tools = await provider.GetToolsForServerAsync(Guid.NewGuid().ToString());

        tools.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshAsync_DoesNotThrow_WhenCacheEmpty()
    {
        var provider = BuildProvider(servers: Array.Empty<McpServerDefinition>(), overrides: Array.Empty<McpToolOverride>());

        var act = () => provider.RefreshAsync();

        await act.Should().NotThrowAsync();
    }

    private static McpToolProvider BuildProvider(
        IReadOnlyList<McpServerDefinition> servers,
        IReadOnlyList<McpToolOverride> overrides)
    {
        var loggerFactory = NullLoggerFactory.Instance;
        var inProcessHost = new InProcessMcpHost(loggerFactory, NullLogger<InProcessMcpHost>.Instance);
        var stdioHost = new StdioMcpHost(new DpapiSecretStore(NullLogger<DpapiSecretStore>.Instance), NullLogger<StdioMcpHost>.Instance);
        var catalog = new StubCatalog(servers, overrides);
        return new McpToolProvider(catalog, inProcessHost, stdioHost, NullLogger<McpToolProvider>.Instance);
    }

    private sealed class StubCatalog : IMcpServerCatalog
    {
        private readonly IReadOnlyList<McpServerDefinition> _servers;
        private readonly IReadOnlyList<McpToolOverride> _overrides;

        public StubCatalog(IReadOnlyList<McpServerDefinition> servers, IReadOnlyList<McpToolOverride> overrides)
        {
            _servers = servers;
            _overrides = overrides;
        }

        public Task<IReadOnlyList<McpServerDefinition>> GetServersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_servers);

        public Task<IReadOnlyList<McpToolOverride>> GetOverridesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_overrides);
    }
}
