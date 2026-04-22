using System.Text.Json;
using FluentAssertions;
using OpenClawNet.Tools.Abstractions;
using OpenClawNet.Tools.Core;

namespace OpenClawNet.UnitTests.Tools;

public class ToolRegistryTests
{
    [Fact]
    public void Register_AddsToolToRegistry()
    {
        var registry = new ToolRegistry();
        var tool = new FakeTool("test_tool");
        
        registry.Register(tool);
        
        registry.GetTool("test_tool").Should().BeSameAs(tool);
    }
    
    [Fact]
    public void GetTool_IsCaseInsensitive()
    {
        var registry = new ToolRegistry();
        var tool = new FakeTool("Test_Tool");
        registry.Register(tool);
        
        registry.GetTool("test_tool").Should().BeSameAs(tool);
        registry.GetTool("TEST_TOOL").Should().BeSameAs(tool);
    }
    
    [Fact]
    public void GetTool_ReturnsNull_WhenNotFound()
    {
        var registry = new ToolRegistry();
        
        registry.GetTool("nonexistent").Should().BeNull();
    }
    
    [Fact]
    public void GetAllTools_ReturnsRegisteredTools()
    {
        var registry = new ToolRegistry();
        registry.Register(new FakeTool("tool1"));
        registry.Register(new FakeTool("tool2"));
        
        registry.GetAllTools().Should().HaveCount(2);
    }
    
    [Fact]
    public void GetToolManifest_ReturnsMetadata()
    {
        var registry = new ToolRegistry();
        registry.Register(new FakeTool("my_tool"));
        
        var manifest = registry.GetToolManifest();
        
        manifest.Should().HaveCount(1);
        manifest[0].Name.Should().Be("my_tool");
    }
    
    private sealed class FakeTool : ITool
    {
        public FakeTool(string name) => Name = name;
        
        public string Name { get; }
        public string Description => $"Fake tool: {Name}";
        
        public ToolMetadata Metadata => new()
        {
            Name = Name,
            Description = Description,
            ParameterSchema = JsonDocument.Parse("{}")
        };
        
        public Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken cancellationToken = default)
            => Task.FromResult(ToolResult.Ok(Name, "ok", TimeSpan.Zero));
    }
}
