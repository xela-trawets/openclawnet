using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenClawNet.Tools.Abstractions;

namespace OpenClawNet.Tools.FileSystem;

public sealed class FileSystemTool : ITool
{
    private readonly ILogger<FileSystemTool> _logger;
    private readonly string _workspaceRoot;

    // Block access to sensitive paths
    private static readonly string[] BlockedPaths = [".env", ".git", "appsettings.Production"];

    public FileSystemTool(ILogger<FileSystemTool> logger, IConfiguration configuration)
    {
        _logger = logger;
        // Prefer explicit config, then walk up from the app base directory to find the solution root
        var configuredPath = configuration["Agent:WorkspacePath"];
        _workspaceRoot = !string.IsNullOrWhiteSpace(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : FindSolutionRoot();
        _logger.LogInformation("FileSystem workspace root: {Root}", _workspaceRoot);
    }

    public string Name => "file_system";
    public string Description => $"Read, write, list files and find .NET projects in the workspace at: {_workspaceRoot}";

    public ToolMetadata Metadata => new()
    {
        Name = Name,
        Description = Description,
        ParameterSchema = JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "action": {
                    "type": "string",
                    "enum": ["read", "write", "list", "find_projects"],
                    "description": "Operation: read=get file content, list=list directory, find_projects=find all .sln/.csproj files, write=write file"
                },
                "path": {
                    "type": "string",
                    "description": "File or directory path. Use '.' for workspace root, or an absolute path. Can be relative or absolute."
                },
                "content": { "type": "string", "description": "Content to write (required for write action)" }
            },
            "required": ["action", "path"]
        }
        """),
        RequiresApproval = true,
        Category = "filesystem",
        Tags = ["file", "read", "write", "list", "projects", "dotnet"]
    };

    public async Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var action = input.GetStringArgument("action");
            var path = input.GetStringArgument("path");

            if (string.IsNullOrEmpty(action) || string.IsNullOrEmpty(path))
            {
                return ToolResult.Fail(Name, "Both 'action' and 'path' are required", sw.Elapsed);
            }

            // Resolve and validate path
            var fullPath = ResolvePath(path);
            if (fullPath is null)
            {
                return ToolResult.Fail(Name, $"Path '{path}' is outside the workspace or blocked", sw.Elapsed);
            }

            return action.ToLowerInvariant() switch
            {
                "read" => await ReadFileAsync(fullPath, sw, cancellationToken),
                "write" => await WriteFileAsync(fullPath, input.GetStringArgument("content") ?? "", sw, cancellationToken),
                "list" => ListDirectory(fullPath, sw),
                "find_projects" => FindProjects(fullPath, sw),
                _ => ToolResult.Fail(Name, $"Unknown action: {action}. Use 'read', 'write', 'list', or 'find_projects'.", sw.Elapsed)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FileSystem tool error");
            return ToolResult.Fail(Name, ex.Message, sw.Elapsed);
        }
    }

    private async Task<ToolResult> ReadFileAsync(string fullPath, System.Diagnostics.Stopwatch sw, CancellationToken ct)
    {
        if (!File.Exists(fullPath))
        {
            return ToolResult.Fail(Name, $"File not found: {fullPath}", sw.Elapsed);
        }

        var info = new FileInfo(fullPath);
        if (info.Length > 1_000_000) // 1MB limit
        {
            return ToolResult.Fail(Name, $"File too large ({info.Length} bytes). Maximum is 1MB.", sw.Elapsed);
        }

        var content = await File.ReadAllTextAsync(fullPath, ct);
        _logger.LogInformation("Read file: {Path} ({Length} chars)", fullPath, content.Length);

        sw.Stop();
        return ToolResult.Ok(Name, content, sw.Elapsed);
    }

    private async Task<ToolResult> WriteFileAsync(string fullPath, string content, System.Diagnostics.Stopwatch sw, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(fullPath);
        if (directory is not null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, content, ct);
        _logger.LogInformation("Wrote file: {Path} ({Length} chars)", fullPath, content.Length);

        sw.Stop();
        return ToolResult.Ok(Name, $"File written successfully: {fullPath} ({content.Length} chars)", sw.Elapsed);
    }

    private ToolResult ListDirectory(string fullPath, System.Diagnostics.Stopwatch sw)
    {
        if (!Directory.Exists(fullPath))
        {
            return ToolResult.Fail(Name, $"Directory not found: {fullPath}", sw.Elapsed);
        }

        var sb = new StringBuilder();

        var dirs = Directory.GetDirectories(fullPath);
        foreach (var dir in dirs.Take(50))
        {
            sb.AppendLine($"[DIR] {Path.GetFileName(dir)}/");
        }

        var files = Directory.GetFiles(fullPath);
        foreach (var file in files.Take(100))
        {
            var info = new FileInfo(file);
            sb.AppendLine($"      {Path.GetFileName(file)} ({info.Length:N0} bytes)");
        }

        if (dirs.Length > 50 || files.Length > 100)
        {
            sb.AppendLine($"... and more ({dirs.Length} total dirs, {files.Length} total files)");
        }

        sw.Stop();
        return ToolResult.Ok(Name, sb.ToString(), sw.Elapsed);
    }

    private ToolResult FindProjects(string searchRoot, System.Diagnostics.Stopwatch sw)
    {
        if (!Directory.Exists(searchRoot))
        {
            return ToolResult.Fail(Name, $"Directory not found: {searchRoot}", sw.Elapsed);
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# .NET Projects in {searchRoot}");
        sb.AppendLine();

        // Find solution files
        var slnFiles = Directory.GetFiles(searchRoot, "*.sln", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(searchRoot, "*.slnx", SearchOption.AllDirectories))
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                     && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .ToArray();

        if (slnFiles.Length > 0)
        {
            sb.AppendLine("## Solution Files");
            foreach (var sln in slnFiles)
                sb.AppendLine($"  {Path.GetRelativePath(searchRoot, sln)}");
            sb.AppendLine();
        }

        // Find project files
        var projFiles = Directory.GetFiles(searchRoot, "*.csproj", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(searchRoot, "*.fsproj", SearchOption.AllDirectories))
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                     && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .OrderBy(f => f)
            .ToArray();

        sb.AppendLine($"## Projects ({projFiles.Length} found)");
        foreach (var proj in projFiles)
        {
            var rel = Path.GetRelativePath(searchRoot, proj);
            var name = Path.GetFileNameWithoutExtension(proj);
            sb.AppendLine($"  - **{name}** → `{rel}`");
        }

        sw.Stop();
        return ToolResult.Ok(Name, sb.ToString(), sw.Elapsed);
    }

    /// <summary>
    /// Walks up from <see cref="AppContext.BaseDirectory"/> until it finds a directory
    /// containing a .slnx or .sln file — that is the solution root.
    /// Falls back to <see cref="Directory.GetCurrentDirectory"/> if nothing is found.
    /// </summary>
    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("*.slnx").Length > 0 || dir.GetFiles("*.sln").Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }
        return Directory.GetCurrentDirectory();
    }

    private string? ResolvePath(string inputPath)
    {
        // Check blocked paths first
        foreach (var blocked in BlockedPaths)
        {
            if (inputPath.Contains(blocked, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Blocked path access attempt: {Path}", inputPath);
                return null;
            }
        }

        string fullPath;

        // If an absolute path is provided, use it directly (user explicitly gave a path)
        if (Path.IsPathRooted(inputPath))
        {
            fullPath = Path.GetFullPath(inputPath);
        }
        else
        {
            // Relative paths are resolved against the workspace root
            fullPath = Path.GetFullPath(Path.Combine(_workspaceRoot, inputPath));

            // Ensure relative paths stay within workspace (prevent directory traversal)
            if (!fullPath.StartsWith(_workspaceRoot, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Path traversal attempt blocked: {Path} -> {FullPath}", inputPath, fullPath);
                return null;
            }
        }

        return fullPath;
    }
}
