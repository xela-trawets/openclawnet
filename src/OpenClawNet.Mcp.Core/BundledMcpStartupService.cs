using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OpenClawNet.Mcp.Core;

/// <summary>
/// On startup, registers the tools exposed by every <see cref="IBundledMcpServerRegistration"/>
/// with the <see cref="InProcessMcpHost"/> and starts the corresponding server. Runs before
/// the agent runtime is ever constructed (which only happens per-request) so tool listings
/// are available the first time <see cref="McpToolProvider"/> is asked.
/// </summary>
public sealed class BundledMcpStartupService : IHostedService
{
    private readonly BundledMcpServerRegistry _registry;
    private readonly InProcessMcpHost _host;
    private readonly IServiceProvider _services;
    private readonly ILogger<BundledMcpStartupService> _logger;

    public BundledMcpStartupService(
        BundledMcpServerRegistry registry,
        InProcessMcpHost host,
        IServiceProvider services,
        ILogger<BundledMcpStartupService> logger)
    {
        _registry = registry;
        _host = host;
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var reg in _registry.All)
        {
            try
            {
                var tools = reg.CreateTools(_services);
                _host.RegisterTools(reg.Definition.Id, tools);
                await _host.StartAsync(reg.Definition, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(
                    "Started bundled MCP server '{ServerName}' with {ToolCount} tool(s).",
                    reg.Definition.Name, tools.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start bundled MCP server '{ServerName}'.", reg.Definition.Name);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
