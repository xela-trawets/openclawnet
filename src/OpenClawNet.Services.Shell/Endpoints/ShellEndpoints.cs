using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;

namespace OpenClawNet.Services.Shell.Endpoints;

public static class ShellEndpoints
{
    private static readonly HashSet<string> BlockedCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "rm", "del", "format", "fdisk", "mkfs", "dd", "shutdown", "reboot",
        "kill", "taskkill", "net", "reg", "regedit", "powershell", "cmd"
    };

    public static void MapShellEndpoints(this WebApplication app)
    {
        app.MapPost("/api/shell/execute", async (
            ShellExecuteRequest request,
            ILogger<ShellExecuteRequest> logger,
            IOptions<ShellOptions> shellOptions) =>
        {
            var options = shellOptions.Value;
            var command = request.Command;
            var workingDir = request.WorkingDirectory;

            if (string.IsNullOrEmpty(command))
                return Results.BadRequest(new ShellExecuteResponse { Success = false, Stderr = "'command' is required" });

            var firstWord = command.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant();
            firstWord = Path.GetFileNameWithoutExtension(firstWord ?? "");
            if (BlockedCommands.Contains(firstWord))
            {
                logger.LogWarning("Blocked unsafe command: {Command}", command);
                return Results.Ok(new ShellExecuteResponse { Success = false, ExitCode = -1, Stderr = $"Command blocked by safety policy: '{command}'" });
            }

            logger.LogInformation("Executing: {Command}", command);
            var isWindows = OperatingSystem.IsWindows();
            var psi = new ProcessStartInfo
            {
                FileName = isWindows ? "cmd.exe" : "/bin/sh",
                Arguments = isWindows ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"",
                WorkingDirectory = workingDir ?? Directory.GetCurrentDirectory(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            var maxOutputLength = options.MaxOutputLength;
            process.OutputDataReceived += (_, e) => { if (e.Data is not null && stdout.Length < maxOutputLength) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null && stderr.Length < maxOutputLength) stderr.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
            try { await process.WaitForExitAsync(cts.Token); }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return Results.Ok(new ShellExecuteResponse { Success = false, TimedOut = true, Stderr = $"Timed out after {options.TimeoutSeconds}s" });
            }

            return Results.Ok(new ShellExecuteResponse
            {
                Success = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                Stdout = stdout.ToString(),
                Stderr = stderr.ToString()
            });
        })
        .WithTags("Shell")
        .WithName("ExecuteShell");
    }
}

public sealed record ShellExecuteRequest(string Command, string? WorkingDirectory = null);
public sealed record ShellExecuteResponse
{
    public bool Success { get; init; }
    public int ExitCode { get; init; }
    public string Stdout { get; init; } = "";
    public string Stderr { get; init; } = "";
    public bool TimedOut { get; init; }
}
