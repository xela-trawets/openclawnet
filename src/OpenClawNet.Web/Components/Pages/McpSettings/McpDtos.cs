namespace OpenClawNet.Web.Components.Pages.McpSettings;

/// <summary>DTOs shared by the MCP settings pages — mirror the Gateway's /api/mcp shapes.</summary>
public static class McpDtos
{
    public sealed record McpServerListItem(
        Guid Id,
        string Name,
        string Transport,
        string? Command,
        string[] Args,
        string? Url,
        bool HasEnv,
        bool HasHeaders,
        bool Enabled,
        bool IsBuiltIn,
        bool IsRunning,
        int ToolCount,
        string? LastError);

    public sealed class ServerWriteRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Transport { get; set; } = "stdio";
        public string? Command { get; set; }
        public List<string>? Args { get; set; }
        public Dictionary<string, string>? Env { get; set; }
        public string? Url { get; set; }
        public Dictionary<string, string>? Headers { get; set; }
        public bool Enabled { get; set; } = true;
    }

    public sealed record TestResult(bool Ok, ToolDescriptor[] Tools, string? Error);

    public sealed record ToolDescriptor(string Name, string? Description);

    public sealed record RegistryEntry(
        string Id,
        string Name,
        string? Description,
        string Transport,
        string? SuggestedCommand,
        string[] SuggestedArgs,
        string? SuggestedUrl,
        string Source);

    public sealed record RegistrySearchResult(RegistryEntry[] Entries, string? NextCursor);

    public sealed record Suggestion(
        string Id,
        string Name,
        string? Description,
        string Transport,
        string? Command,
        string[] Args,
        string? Url,
        string? Category,
        string? Homepage,
        string[] RequiresEnv);
}
