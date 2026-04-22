using Microsoft.Extensions.Logging;

namespace OpenClawNet.Skills;

public sealed class FileSkillLoader : ISkillLoader
{
    private readonly ILogger<FileSkillLoader> _logger;
    private readonly List<string> _skillDirectories;
    private readonly string _installedSkillsDirectory;
    private readonly object _lock = new();
    private List<LoadedSkill> _skills = [];
    private readonly HashSet<string> _disabledSkills = new(StringComparer.OrdinalIgnoreCase);
    
    public FileSkillLoader(ILogger<FileSkillLoader> logger, IEnumerable<string>? skillDirectories = null)
    {
        _logger = logger;
        _skillDirectories = skillDirectories?.ToList() ?? ["skills/built-in", "skills/samples"];
        _installedSkillsDirectory = "skills/installed";
        if (!_skillDirectories.Contains(_installedSkillsDirectory, StringComparer.OrdinalIgnoreCase))
            _skillDirectories.Add(_installedSkillsDirectory);
    }

    /// <summary>Overload used in tests (and advanced DI) to specify a custom installed-skills directory.</summary>
    public FileSkillLoader(ILogger<FileSkillLoader> logger, IEnumerable<string>? skillDirectories, string installedSkillsDirectory)
    {
        _logger = logger;
        _installedSkillsDirectory = Path.GetFullPath(installedSkillsDirectory);
        _skillDirectories = skillDirectories?.ToList() ?? [];
        if (!_skillDirectories.Contains(_installedSkillsDirectory, StringComparer.OrdinalIgnoreCase))
            _skillDirectories.Add(_installedSkillsDirectory);
    }
    
    public async Task<IReadOnlyList<SkillContent>> GetActiveSkillsAsync(CancellationToken cancellationToken = default)
    {
        if (_skills.Count == 0)
            await ReloadAsync(cancellationToken);
        
        lock (_lock)
        {
            return _skills
                .Where(s => s.Definition.Enabled && !_disabledSkills.Contains(s.Definition.Name))
                .Select(s => new SkillContent
                {
                    Name = s.Definition.Name,
                    Content = s.Content,
                    Description = s.Definition.Description,
                    Tags = s.Definition.Tags
                })
                .ToList();
        }
    }
    
    public async Task<IReadOnlyList<SkillDefinition>> ListSkillsAsync(CancellationToken cancellationToken = default)
    {
        if (_skills.Count == 0)
            await ReloadAsync(cancellationToken);
        
        lock (_lock)
        {
            return _skills.Select(s => s.Definition with
            {
                Enabled = s.Definition.Enabled && !_disabledSkills.Contains(s.Definition.Name)
            }).ToList();
        }
    }
    
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        var loaded = new Dictionary<string, LoadedSkill>(StringComparer.OrdinalIgnoreCase);
        
        // Load in directory order — later directories override earlier ones
        foreach (var dir in _skillDirectories)
        {
            if (!Directory.Exists(dir))
            {
                _logger.LogDebug("Skill directory not found: {Directory}", dir);
                continue;
            }
            
            // Load flat .md files in the directory
            foreach (var file in Directory.GetFiles(dir, "*.md"))
            {
                await LoadSkillFileAsync(file, loaded, cancellationToken);
            }

            // Load awesome-copilot style subdirectories: skills/<name>/SKILL.md
            foreach (var subDir in Directory.GetDirectories(dir))
            {
                var skillFile = Path.Combine(subDir, "SKILL.md");
                if (File.Exists(skillFile))
                {
                    await LoadSkillFileAsync(skillFile, loaded, cancellationToken);
                }
            }
        }
        
        lock (_lock)
        {
            _skills = loaded.Values.ToList();
        }
        
        _logger.LogInformation("Loaded {Count} skills from {DirCount} directories", loaded.Count, _skillDirectories.Count);
    }

    public async Task InstallSkillAsync(string name, string content, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_installedSkillsDirectory))
            Directory.CreateDirectory(_installedSkillsDirectory);

        // Sanitize the skill name for use as a directory name
        var safeName = string.Concat(name.Split(Path.GetInvalidFileNameChars()));
        var skillDir = Path.Combine(_installedSkillsDirectory, safeName);
        Directory.CreateDirectory(skillDir);

        var skillFile = Path.Combine(skillDir, "SKILL.md");
        await File.WriteAllTextAsync(skillFile, content, cancellationToken);
        _logger.LogInformation("Installed skill: {Name} -> {Path}", name, skillFile);

        await ReloadAsync(cancellationToken);
    }

    public async Task UninstallSkillAsync(string name, CancellationToken cancellationToken = default)
    {
        var safeName = string.Concat(name.Split(Path.GetInvalidFileNameChars()));
        var skillDir = Path.Combine(_installedSkillsDirectory, safeName);

        if (Directory.Exists(skillDir))
        {
            Directory.Delete(skillDir, recursive: true);
            _logger.LogInformation("Uninstalled skill directory: {Dir}", skillDir);
        }
        else
        {
            // Also try flat file
            var flatFile = Path.Combine(_installedSkillsDirectory, $"{safeName}.md");
            if (File.Exists(flatFile))
            {
                File.Delete(flatFile);
                _logger.LogInformation("Uninstalled skill file: {File}", flatFile);
            }
            else
            {
                _logger.LogWarning("Skill not found for uninstall: {Name}", name);
            }
        }

        await ReloadAsync(cancellationToken);
        await Task.CompletedTask;
    }
    
    public void EnableSkill(string name)
    {
        lock (_lock) { _disabledSkills.Remove(name); }
        _logger.LogInformation("Enabled skill: {Name}", name);
    }
    
    public void DisableSkill(string name)
    {
        lock (_lock) { _disabledSkills.Add(name); }
        _logger.LogInformation("Disabled skill: {Name}", name);
    }

    private async Task LoadSkillFileAsync(string file, Dictionary<string, LoadedSkill> loaded, CancellationToken cancellationToken)
    {
        try
        {
            var content = await File.ReadAllTextAsync(file, cancellationToken);
            var (definition, skillContent) = SkillParser.Parse(file, content);

            // Mark skills from the installed directory
            var isInstalled = file.StartsWith(
                Path.GetFullPath(_installedSkillsDirectory),
                StringComparison.OrdinalIgnoreCase);
            if (isInstalled)
                definition = definition with { Source = "installed" };

            loaded[definition.Name] = new LoadedSkill(definition, skillContent);
            _logger.LogInformation("Loaded skill: {Name} from {Path}", definition.Name, file);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load skill from {Path}", file);
        }
    }
    
    private sealed record LoadedSkill(SkillDefinition Definition, string Content);
}
