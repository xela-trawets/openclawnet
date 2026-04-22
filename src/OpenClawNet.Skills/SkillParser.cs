using System.Text.RegularExpressions;

namespace OpenClawNet.Skills;

public static partial class SkillParser
{
    [GeneratedRegex(@"^---\s*\n(.*?)\n---\s*\n(.*)$", RegexOptions.Singleline)]
    private static partial Regex FrontmatterRegex();
    
    public static (SkillDefinition Definition, string Content) Parse(string filePath, string rawContent)
    {
        var match = FrontmatterRegex().Match(rawContent);
        
        string content;
        var name = Path.GetFileNameWithoutExtension(filePath);
        string? description = null;
        string? category = null;
        var tags = new List<string>();
        var examples = new List<string>();
        var enabled = true;
        var inTags = false;
        var inExamples = false;
        
        if (match.Success)
        {
            var yaml = match.Groups[1].Value;
            content = match.Groups[2].Value.Trim();
            
            foreach (var line in yaml.Split('\n'))
            {
                var trimmed = line.Trim();
                
                // Track which list we're in
                if (trimmed.StartsWith("tags:"))
                {
                    inTags = true;
                    inExamples = false;
                    var inline = trimmed["tags:".Length..].Trim().Trim('[', ']');
                    if (!string.IsNullOrEmpty(inline))
                    {
                        tags.AddRange(inline.Split(',').Select(t => t.Trim().Trim('"', '\'')));
                        inTags = false;
                    }
                    continue;
                }
                if (trimmed.StartsWith("examples:"))
                {
                    inExamples = true;
                    inTags = false;
                    continue;
                }
                
                if (trimmed.StartsWith("- "))
                {
                    var value = trimmed[2..].Trim().Trim('"', '\'');
                    if (inTags) tags.Add(value);
                    else if (inExamples) examples.Add(value);
                    continue;
                }
                
                // Non-list items reset context
                if (!trimmed.StartsWith("- "))
                {
                    inTags = false;
                    inExamples = false;
                }
                
                if (trimmed.StartsWith("name:"))
                    name = trimmed["name:".Length..].Trim().Trim('"', '\'');
                else if (trimmed.StartsWith("description:"))
                    description = trimmed["description:".Length..].Trim().Trim('"', '\'');
                else if (trimmed.StartsWith("category:"))
                    category = trimmed["category:".Length..].Trim().Trim('"', '\'');
                else if (trimmed.StartsWith("enabled:"))
                    enabled = trimmed["enabled:".Length..].Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
            }
        }
        else
        {
            content = rawContent.Trim();
        }
        
        var definition = new SkillDefinition
        {
            Name = name,
            Description = description,
            Category = category,
            Tags = tags,
            Enabled = enabled,
            FilePath = filePath,
            Examples = examples
        };
        
        return (definition, content);
    }
}
