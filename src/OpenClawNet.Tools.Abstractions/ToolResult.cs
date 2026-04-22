namespace OpenClawNet.Tools.Abstractions;

public sealed record ToolResult
{
    public required string ToolName { get; init; }
    public required bool Success { get; init; }
    public required string Output { get; init; }
    public string? Error { get; init; }
    public TimeSpan Duration { get; init; }
    
    public static ToolResult Ok(string toolName, string output, TimeSpan duration) =>
        new() { ToolName = toolName, Success = true, Output = output, Duration = duration };
    
    public static ToolResult Fail(string toolName, string error, TimeSpan duration) =>
        new() { ToolName = toolName, Success = false, Output = string.Empty, Error = error, Duration = duration };
}
