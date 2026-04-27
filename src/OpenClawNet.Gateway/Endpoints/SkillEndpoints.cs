using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using OpenClawNet.Skills;

namespace OpenClawNet.Gateway.Endpoints;

public static class SkillEndpoints
{
    private const string AwesomeCopilotApiUrl = "https://api.github.com/repos/github/awesome-copilot/contents/skills";
    private const string AwesomeCopilotRawBase = "https://raw.githubusercontent.com/github/awesome-copilot/main/skills";

    public static void MapSkillEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/skills").WithTags("Skills");

        group.MapGet("/", async (ISkillLoader loader) =>
        {
            var skills = await loader.ListSkillsAsync();
            return Results.Ok(skills);
        })
        .WithName("ListSkills");

        group.MapPost("/reload", async (ISkillLoader loader) =>
        {
            await loader.ReloadAsync();
            var skills = await loader.ListSkillsAsync();
            return Results.Ok(new { reloaded = true, count = skills.Count });
        })
        .WithName("ReloadSkills");

        group.MapPost("/{name}/enable", (string name, ISkillLoader loader) =>
        {
            loader.EnableSkill(name);
            return Results.Ok(new { name, enabled = true });
        })
        .WithName("EnableSkill");

        group.MapPost("/{name}/disable", (string name, ISkillLoader loader) =>
        {
            loader.DisableSkill(name);
            return Results.Ok(new { name, enabled = false });
        })
        .WithName("DisableSkill");

        // ── Import Endpoint ──────────────────────────────────────────────────────────────

        group.MapPost("/import", async (HttpRequest request, ISkillLoader loader) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest(new { success = false, error = "Content-Type must be multipart/form-data" });

            var form = await request.ReadFormAsync();
            var file = form.Files.FirstOrDefault();
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { success = false, error = "No file provided" });

            try
            {
                string skillName;
                string skillContent;

                if (file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    // Handle zip file
                    using var stream = file.OpenReadStream();
                    using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

                    var skillMdEntry = archive.Entries.FirstOrDefault(e => 
                        e.Name.Equals("SKILL.md", StringComparison.OrdinalIgnoreCase) && 
                        !e.FullName.Contains("/"));

                    if (skillMdEntry == null)
                        return Results.BadRequest(new { success = false, error = "Zip file must contain SKILL.md at root level" });

                    using var entryStream = skillMdEntry.Open();
                    using var reader = new StreamReader(entryStream);
                    skillContent = await reader.ReadToEndAsync();

                    // Extract skill name from zip filename (without .zip extension)
                    skillName = Path.GetFileNameWithoutExtension(file.FileName);
                }
                else if (file.FileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    // Handle markdown file
                    using var stream = file.OpenReadStream();
                    using var reader = new StreamReader(stream);
                    skillContent = await reader.ReadToEndAsync();

                    // Extract skill name from filename (without .md extension)
                    skillName = Path.GetFileNameWithoutExtension(file.FileName);
                }
                else
                {
                    return Results.BadRequest(new { success = false, error = "Only .md and .zip files are supported" });
                }

                // Check if skill already exists
                var existingSkills = await loader.ListSkillsAsync();
                if (existingSkills.Any(s => s.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase)))
                    return Results.BadRequest(new 
                    { 
                        success = false, 
                        error = "Skill already exists. Delete or rename existing skill first." 
                    });

                await loader.InstallSkillAsync(skillName, skillContent);

                return Results.Ok(new 
                { 
                    success = true, 
                    skillName = skillName, 
                    message = $"Skill '{skillName}' imported successfully" 
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new 
                { 
                    success = false, 
                    error = $"Failed to import skill: {ex.Message}" 
                });
            }
        })
        .WithName("ImportSkills");

        // ── Marketplace ──────────────────────────────────────────────────────────────

        group.MapGet("/marketplace", async (IHttpClientFactory httpClientFactory) =>
        {
            try
            {
                var client = httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("User-Agent", "OpenClawNet/1.0");

                var response = await client.GetAsync(AwesomeCopilotApiUrl);
                if (!response.IsSuccessStatusCode)
                    return Results.Problem($"GitHub API returned {response.StatusCode}", statusCode: (int)response.StatusCode);

                var json = await response.Content.ReadAsStringAsync();
                var entries = JsonSerializer.Deserialize<GitHubContentEntry[]>(json, JsonOptions);
                if (entries is null)
                    return Results.Ok(Array.Empty<MarketplaceSkill>());

                var skillDirs = entries.Where(e => e.Type == "dir").ToArray();
                return Results.Ok(skillDirs.Select(d => new MarketplaceSkill(
                    d.Name,
                    $"{AwesomeCopilotRawBase}/{d.Name}/SKILL.md",
                    $"https://github.com/github/awesome-copilot/tree/main/skills/{d.Name}"
                )));
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to fetch marketplace: {ex.Message}", statusCode: 500);
            }
        })
        .WithName("GetMarketplaceSkills");

        group.MapPost("/install", async (InstallSkillRequest request, ISkillLoader loader, IHttpClientFactory httpClientFactory) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.ContentUrl))
                return Results.BadRequest("Name and ContentUrl are required.");

            try
            {
                var client = httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("User-Agent", "OpenClawNet/1.0");

                var content = await client.GetStringAsync(request.ContentUrl);
                await loader.InstallSkillAsync(request.Name, content);

                return Results.Ok(new { name = request.Name, installed = true });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to install skill '{request.Name}': {ex.Message}", statusCode: 500);
            }
        })
        .WithName("InstallSkill");

        group.MapDelete("/installed/{name}", async (string name, ISkillLoader loader) =>
        {
            await loader.UninstallSkillAsync(name);
            return Results.Ok(new { name, uninstalled = true });
        })
        .WithName("UninstallSkill");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record GitHubContentEntry(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("html_url")] string HtmlUrl);

    private sealed record MarketplaceSkill(string Name, string ContentUrl, string GitHubUrl);

    private sealed record InstallSkillRequest(string Name, string ContentUrl);
}
