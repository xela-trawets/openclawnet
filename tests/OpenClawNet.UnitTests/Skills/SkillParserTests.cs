using FluentAssertions;
using OpenClawNet.Skills;

namespace OpenClawNet.UnitTests.Skills;

public class SkillParserTests
{
    [Fact]
    public void Parse_WithValidFrontmatter_ExtractsMetadata()
    {
        var content = """
            ---
            name: test-skill
            description: A test skill
            category: testing
            enabled: true
            tags:
              - test
              - unit
            ---
            
            You are a testing assistant.
            """;
        
        var (definition, body) = SkillParser.Parse("test-skill.md", content);
        
        definition.Name.Should().Be("test-skill");
        definition.Description.Should().Be("A test skill");
        definition.Category.Should().Be("testing");
        definition.Enabled.Should().BeTrue();
        definition.Tags.Should().Contain("test");
        definition.Tags.Should().Contain("unit");
        body.Should().Contain("testing assistant");
    }
    
    [Fact]
    public void Parse_WithoutFrontmatter_UsesFileName()
    {
        var content = "Just plain content without frontmatter.";
        
        var (definition, body) = SkillParser.Parse("my-skill.md", content);
        
        definition.Name.Should().Be("my-skill");
        definition.Enabled.Should().BeTrue();
        body.Should().Be("Just plain content without frontmatter.");
    }
    
    [Fact]
    public void Parse_WithDisabledFlag_SetsEnabled()
    {
        var content = """
            ---
            name: disabled-skill
            enabled: false
            ---
            
            This skill is disabled.
            """;
        
        var (definition, _) = SkillParser.Parse("disabled.md", content);
        
        definition.Enabled.Should().BeFalse();
    }
    
    [Fact]
    public void Parse_WithEmptyContent_ReturnsEmptyBody()
    {
        var content = "---\nname: empty\n---\n";
        
        var (definition, body) = SkillParser.Parse("empty.md", content);
        
        definition.Name.Should().Be("empty");
        body.Should().BeEmpty();
    }
}
