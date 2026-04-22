using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenClawNet.Mcp.Abstractions;

namespace OpenClawNet.Mcp.Core;

/// <summary>
/// On startup, reads every enabled <see cref="McpServerDefinition"/> from the catalog
/// and starts the matching host. PR-A only handles InProcess + Stdio; HTTP transport
/// lands with the settings UI in PR-C.
/// </summary>
public sealed class McpServerLifecycleService : IHostedService
{
    private readonly IMcpServerCatalog _catalog;
    private readonly InProcessMcpHost _inProcessHost;
    private readonly StdioMcpHost _stdioHost;
    private readonly ILogger<McpServerLifecycleService> _logger;

    public McpServerLifecycleService(
        IMcpServerCatalog catalog,
        InProcessMcpHost inProcessHost,
        StdioMcpHost stdioHost,
        ILogger<McpServerLifecycleService> logger)
    {
        _catalog = catalog;
        _inProcessHost = inProcessHost;
        _stdioHost = stdioHost;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<McpServerDefinition> definitions;
        try
        {
            definitions = await _catalog.GetServersAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Don't take down the host because the MCP table is empty/missing.
            _logger.LogError(ex, "Could not read MCP server definitions; skipping startup.");
            return;
        }

        foreach (var def in definitions.Where(d => d.Enabled))
        {
            try
            {
                switch (def.Transport)
                {
                    case McpTransport.InProcess:
                        await _inProcessHost.StartAsync(def, cancellationToken).ConfigureAwait(false);
                        break;
                    case McpTransport.Stdio:
                        await _stdioHost.StartAsync(def, cancellationToken).ConfigureAwait(false);
                        break;
                    case McpTransport.Http:
                        _logger.LogInformation("MCP server '{ServerName}' uses HTTP transport — skipped (handled in PR-C).", def.Name);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start MCP server '{ServerName}'.", def.Name);
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _inProcessHost.DisposeAsync().ConfigureAwait(false);
        await _stdioHost.DisposeAsync().ConfigureAwait(false);
    }
}
