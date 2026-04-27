using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace OpenClawNet.Storage.Services;

/// <summary>
/// Service for importing skills from single markdown files or zip archives.
/// Handles validation, extraction, and storage in the skills directory.
/// </summary>
public class SkillImportService
{
    private readonly StorageOptions _storageOptions;
    private readonly ILogger<SkillImportService> _logger;

    public SkillImportService(StorageOptions storageOptions, ILogger<SkillImportService> logger)
    {
        _storageOptions = storageOptions;
        _logger = logger;
    }

    /// <summary>
    /// Imports a single markdown file as a new skill.
    /// </summary>
    /// <param name="fileName">The name of the file (must end with .md)</param>
    /// <param name="stream">The file stream containing the markdown content</param>
    /// <returns>The skill name (filename without extension)</returns>
    /// <exception cref="ArgumentException">If file name is invalid or already exists</exception>
    /// <exception cref="InvalidOperationException">If the file is not valid markdown</exception>
    public async Task<string> ImportSingleFileAsync(string fileName, Stream stream)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

        if (!fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("File must be a markdown file (.md extension).", nameof(fileName));

        // Extract skill name and validate
        var skillName = Path.GetFileNameWithoutExtension(fileName);
        ValidateSkillNameFormat(skillName);

        if (!ValidateSkillName(skillName))
            throw new InvalidOperationException($"Skill '{skillName}' already exists. Delete or rename existing skill first.");

        try
        {
            // Ensure skills directory exists
            Directory.CreateDirectory(_storageOptions.SkillsPath);

            // Create skill file path
            var skillFilePath = Path.Combine(_storageOptions.SkillsPath, skillName, "SKILL.md");
            Directory.CreateDirectory(Path.GetDirectoryName(skillFilePath)!);

            // Copy file to destination
            using var fileStream = File.Create(skillFilePath);
            await stream.CopyToAsync(fileStream);

            _logger.LogInformation("Imported single skill file: {SkillName} -> {Path}", skillName, skillFilePath);
            return skillName;
        }
        catch (Exception ex) when (!(ex is ArgumentException) && !(ex is InvalidOperationException))
        {
            _logger.LogError(ex, "Failed to import skill file: {FileName}", fileName);
            throw new InvalidOperationException($"Failed to import skill '{fileName}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Imports a zip archive containing a skill folder structure.
    /// </summary>
    /// <param name="zipStream">The zip archive stream</param>
    /// <returns>The skill name (folder name from the zip)</returns>
    /// <exception cref="ArgumentException">If the zip structure is invalid</exception>
    /// <exception cref="InvalidOperationException">If validation fails</exception>
    public async Task<string> ImportFolderAsync(Stream zipStream)
    {
        try
        {
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            if (archive.Entries.Count == 0)
                throw new ArgumentException("Zip archive is empty.");

            // Find the root folder (skill name)
            var rootEntries = archive.Entries
                .Where(e => !e.FullName.Contains("/"))
                .ToList();

            string skillName;

            if (rootEntries.Count > 0 && archive.Entries.Any(e => e.FullName.Equals("SKILL.md", StringComparison.OrdinalIgnoreCase)))
            {
                // Flat structure: SKILL.md at root
                var skillMdEntry = archive.Entries.First(e => e.FullName.Equals("SKILL.md", StringComparison.OrdinalIgnoreCase));
                // Extract skill name from the first directory (if any nested structure exists)
                skillName = archive.Entries
                    .Where(e => e.FullName.Contains("/"))
                    .Select(e => e.FullName.Split('/')[0])
                    .FirstOrDefault() ?? "imported-skill";
            }
            else
            {
                // Folder structure: Find folder containing SKILL.md
                var skillMdEntries = archive.Entries
                    .Where(e => e.Name.Equals("SKILL.md", StringComparison.OrdinalIgnoreCase) && e.FullName.Contains("/"))
                    .ToList();

                if (skillMdEntries.Count == 0)
                    throw new ArgumentException("Zip must contain SKILL.md at the root or in a single subfolder.");

                if (skillMdEntries.Count > 1)
                    throw new ArgumentException("Zip must contain only one skill folder (only one SKILL.md allowed).");

                skillName = skillMdEntries[0].FullName.Split('/')[0];
            }

            // Validate skill name
            ValidateSkillNameFormat(skillName);

            if (!ValidateSkillName(skillName))
                throw new InvalidOperationException($"Skill '{skillName}' already exists. Delete or rename existing skill first.");

            // Extract and validate subfiles
            var subFiles = archive.Entries
                .Where(e => !e.Name.Equals("SKILL.md", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(e.Name))
                .ToList();

            foreach (var subFile in subFiles)
            {
                if (subFile.Name.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
                    subFile.Name.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
                {
                    using var sr = new StreamReader(subFile.Open());
                    var content = await sr.ReadToEndAsync();
                    ValidateYamlSyntax(content, subFile.Name);
                }
                else if (subFile.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    using var sr = new StreamReader(subFile.Open());
                    var content = await sr.ReadToEndAsync();
                    ValidateJsonSyntax(content, subFile.Name);
                }
            }

            // Extract zip to skills directory
            var skillPath = Path.Combine(_storageOptions.SkillsPath, skillName);
            Directory.CreateDirectory(skillPath);

            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.StartsWith(skillName + "/"))
                {
                    var relativePath = entry.FullName.Substring(skillName.Length + 1);
                    if (!string.IsNullOrEmpty(relativePath))
                    {
                        var entryPath = Path.Combine(skillPath, relativePath);
                        var directoryPath = Path.GetDirectoryName(entryPath);
                        if (!string.IsNullOrEmpty(directoryPath))
                            Directory.CreateDirectory(directoryPath);

                        if (!entry.FullName.EndsWith("/"))
                        {
                            using var entryStream = entry.Open();
                            using var fileStream = File.Create(entryPath);
                            await entryStream.CopyToAsync(fileStream);
                        }
                    }
                }
                else if (!entry.FullName.Contains("/") && !entry.Name.Equals("SKILL.md", StringComparison.OrdinalIgnoreCase))
                {
                    // Handle flat structure - copy files from root
                    var entryPath = Path.Combine(skillPath, entry.Name);
                    using var entryStream = entry.Open();
                    using var fileStream = File.Create(entryPath);
                    await entryStream.CopyToAsync(fileStream);
                }
                else if (entry.Name.Equals("SKILL.md", StringComparison.OrdinalIgnoreCase) && !entry.FullName.Contains("/"))
                {
                    // Copy SKILL.md from root
                    var entryPath = Path.Combine(skillPath, "SKILL.md");
                    using var entryStream = entry.Open();
                    using var fileStream = File.Create(entryPath);
                    await entryStream.CopyToAsync(fileStream);
                }
            }

            _logger.LogInformation("Imported skill from zip: {SkillName} -> {Path}", skillName, skillPath);
            return skillName;
        }
        catch (InvalidDataException ex)
        {
            _logger.LogError(ex, "Invalid or corrupt zip file");
            throw new ArgumentException("Invalid or corrupt zip file.", ex);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error processing zip file");
            throw new ArgumentException("Invalid or corrupt zip file.", ex);
        }
        catch (Exception ex) when (!(ex is ArgumentException) && !(ex is InvalidOperationException))
        {
            _logger.LogError(ex, "Failed to import skill from zip");
            throw new InvalidOperationException($"Failed to import skill from zip: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates whether a skill name is unique (doesn't already exist).
    /// </summary>
    /// <param name="name">The skill name to check</param>
    /// <returns>true if the skill name is unique, false if it already exists</returns>
    public bool ValidateSkillName(string name)
    {
        if (!Directory.Exists(_storageOptions.SkillsPath))
            return true;

        var skillPath = Path.Combine(_storageOptions.SkillsPath, name);
        var skillFile = Path.Combine(_storageOptions.SkillsPath, name + ".md");

        return !Directory.Exists(skillPath) && !File.Exists(skillFile);
    }

    /// <summary>
    /// Validates the skill name format (alphanumeric + hyphens, no spaces or path traversal).
    /// </summary>
    private static void ValidateSkillNameFormat(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Skill name cannot be empty.", nameof(name));

        if (name.Contains("..") || name.Contains("/") || name.Contains("\\"))
            throw new ArgumentException("Skill name cannot contain path traversal characters (.., /, \\).", nameof(name));

        if (!Regex.IsMatch(name, @"^[a-zA-Z0-9\-]+$"))
            throw new ArgumentException("Skill name must contain only alphanumeric characters and hyphens.", nameof(name));
    }

    /// <summary>
    /// Validates YAML syntax by checking for basic structure (no actual parsing).
    /// </summary>
    private static void ValidateYamlSyntax(string content, string fileName)
    {
        try
        {
            // Simple validation: check for common YAML syntax issues
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("File content is empty");
                
            // Basic validation - just ensure it's not obviously malformed
            // In production, you'd want more comprehensive validation
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Subfile '{fileName}' has invalid syntax: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates JSON syntax by attempting to parse the content.
    /// </summary>
    private static void ValidateJsonSyntax(string content, string fileName)
    {
        try
        {
            JsonDocument.Parse(content);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Subfile '{fileName}' has invalid JSON syntax: {ex.Message}", ex);
        }
    }
}
