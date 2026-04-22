namespace OpenClawNet.Gateway.Services.Mcp;

/// <summary>
/// One result row from the official MCP registry, normalized into a shape the
/// Settings UI can drop straight into the create-server form.
/// </summary>
public sealed record McpRegistryEntry(
    string Id,
    string Name,
    string? Description,
    string Transport,
    string? SuggestedCommand,
    IReadOnlyList<string> SuggestedArgs,
    string? SuggestedUrl,
    string Source = "registry");

public sealed record McpRegistrySearchResult(
    IReadOnlyList<McpRegistryEntry> Entries,
    string? NextCursor);

/// <summary>
/// Abstraction over the MCP registry so the UI can render gracefully when the
/// registry is unreachable (e.g. offline dev, CI without internet).
/// </summary>
public interface IMcpRegistryClient
{
    Task<McpRegistrySearchResult> SearchAsync(string? query, string? cursor, int limit, CancellationToken cancellationToken);
}
