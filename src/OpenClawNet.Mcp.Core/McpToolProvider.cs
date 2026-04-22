using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using OpenClawNet.Mcp.Abstractions;

namespace OpenClawNet.Mcp.Core;

/// <summary>
/// Aggregates tools from every running MCP server (regardless of transport)
/// into one <see cref="AITool"/> catalog the agent runtime can consume.
/// </summary>
/// <remarks>
/// <para>Tool naming:</para>
/// <list type="bullet">
///   <item>Storage form (used in <c>AgentProfile.EnabledTools</c>): <c>&lt;serverPrefix&gt;.&lt;toolName&gt;</c>.</item>
///   <item>LLM-wire form (returned by <see cref="AITool.Name"/>): <c>&lt;serverPrefix&gt;_&lt;toolName&gt;</c> —
///         most providers reject dots in function names.</item>
/// </list>
/// <para>The cache is keyed on server id and invalidated by <see cref="RefreshAsync"/>.
/// PR-A keeps it dead simple — no TTL, no background refresh.</para>
/// </remarks>
public sealed class McpToolProvider : IMcpToolProvider
{
    private readonly IMcpServerCatalog _catalog;
    private readonly InProcessMcpHost _inProcessHost;
    private readonly StdioMcpHost _stdioHost;
    private readonly ILogger<McpToolProvider> _logger;
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<AITool>> _cache = new();

    public McpToolProvider(
        IMcpServerCatalog catalog,
        InProcessMcpHost inProcessHost,
        StdioMcpHost stdioHost,
        ILogger<McpToolProvider> logger)
    {
        _catalog = catalog;
        _inProcessHost = inProcessHost;
        _stdioHost = stdioHost;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AITool>> GetAllToolsAsync(CancellationToken cancellationToken = default)
    {
        var servers = await _catalog.GetServersAsync(cancellationToken).ConfigureAwait(false);
        var overrides = await _catalog.GetOverridesAsync(cancellationToken).ConfigureAwait(false);
        var overrideLookup = BuildOverrideLookup(overrides);

        var aggregate = new List<AITool>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var server in servers)
        {
            if (!server.Enabled) continue;

            var tools = await BuildToolsForServerAsync(server, overrideLookup, cancellationToken).ConfigureAwait(false);
            foreach (var tool in tools)
            {
                if (seenNames.Add(tool.Name))
                    aggregate.Add(tool);
                else
                    _logger.LogDebug("Duplicate MCP tool name '{ToolName}' from server '{ServerName}' ignored.", tool.Name, server.Name);
            }
        }

        return aggregate;
    }

    public async Task<IReadOnlyList<AITool>> GetToolsForServerAsync(string serverId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(serverId, out var id)) return Array.Empty<AITool>();

        var servers = await _catalog.GetServersAsync(cancellationToken).ConfigureAwait(false);
        var server = servers.FirstOrDefault(s => s.Id == id);
        if (server is null || !server.Enabled) return Array.Empty<AITool>();

        var overrides = await _catalog.GetOverridesAsync(cancellationToken).ConfigureAwait(false);
        return await BuildToolsForServerAsync(server, BuildOverrideLookup(overrides), cancellationToken).ConfigureAwait(false);
    }

    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        _cache.Clear();
        return Task.CompletedTask;
    }

    private async Task<IReadOnlyList<AITool>> BuildToolsForServerAsync(
        McpServerDefinition server,
        IReadOnlyDictionary<(Guid serverId, string toolName), McpToolOverride> overrides,
        CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(server.Id, out var cached))
            return cached;

        var client = ResolveClient(server);
        if (client is null) return Array.Empty<AITool>();

        IList<McpClientTool> rawTools;
        try
        {
            rawTools = await client.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ListTools failed for MCP server '{ServerName}'.", server.Name);
            return Array.Empty<AITool>();
        }

        var prefix = SlugifyServerName(server.Name);
        var visible = new List<AITool>(rawTools.Count);
        foreach (var raw in rawTools)
        {
            if (overrides.TryGetValue((server.Id, raw.Name), out var ov) && ov.Disabled)
                continue;

            visible.Add(new PrefixedMcpTool(raw, prefix, server.Id, server.Name));
        }

        _cache[server.Id] = visible;
        return visible;
    }

    private McpClient? ResolveClient(McpServerDefinition server) => server.Transport switch
    {
        McpTransport.InProcess => _inProcessHost.GetClient(server.Id),
        McpTransport.Stdio => _stdioHost.GetClient(server.Id),
        // HTTP transport lands in PR-C alongside the settings page that creates them.
        _ => null,
    };

    private static IReadOnlyDictionary<(Guid serverId, string toolName), McpToolOverride> BuildOverrideLookup(
        IReadOnlyList<McpToolOverride> overrides)
    {
        var dict = new Dictionary<(Guid, string), McpToolOverride>(overrides.Count);
        foreach (var ov in overrides)
            dict[(ov.ServerId, ov.ToolName)] = ov;
        return dict;
    }

    private static string SlugifyServerName(string name)
    {
        // Lowercase + replace non-alphanumeric with '_' so the storage form
        // <prefix>.<tool> stays parseable and the wire form <prefix>_<tool>
        // satisfies the OpenAI tool-name regex (^[a-zA-Z0-9_-]+$).
        var buf = new char[name.Length];
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            buf[i] = char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_';
        }
        return new string(buf);
    }

    /// <summary>
    /// Wraps an <see cref="McpClientTool"/> with a server-prefixed name. The dot-form
    /// is exposed via <see cref="StorageName"/>; <see cref="Name"/> uses the underscore
    /// form for over-the-wire compatibility with chat-completion APIs.
    /// </summary>
    internal sealed class PrefixedMcpTool : AIFunction, IMcpAITool
    {
        private readonly McpClientTool _inner;
        private readonly string _prefix;
        private readonly string _name;

        public PrefixedMcpTool(McpClientTool inner, string prefix, Guid serverId, string serverName)
        {
            _inner = inner;
            _prefix = prefix;
            _name = $"{prefix}_{inner.Name}";
            StorageName = $"{prefix}.{inner.Name}";
            ServerId = serverId;
            ServerName = serverName;
        }

        public string StorageName { get; }

        public Guid ServerId { get; }

        public string ServerName { get; }

        public override string Name => _name;

        public override string Description => _inner.Description;

        public override JsonElement JsonSchema => _inner.JsonSchema;

        protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
            => _inner.InvokeAsync(arguments, cancellationToken);
    }
}
