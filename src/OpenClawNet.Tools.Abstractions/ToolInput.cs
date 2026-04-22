using System.Text.Json;

namespace OpenClawNet.Tools.Abstractions;

public sealed record ToolInput
{
    public required string ToolName { get; init; }
    public required string RawArguments { get; init; }
    
    public T? GetArgument<T>(string key)
    {
        using var doc = JsonDocument.Parse(RawArguments);
        if (doc.RootElement.TryGetProperty(key, out var value))
        {
            return JsonSerializer.Deserialize<T>(value.GetRawText());
        }
        return default;
    }
    
    public string? GetStringArgument(string key)
    {
        using var doc = JsonDocument.Parse(RawArguments);
        if (doc.RootElement.TryGetProperty(key, out var value))
        {
            return value.GetString();
        }
        return null;
    }
}
