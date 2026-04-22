using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenClawNet.Tools.Abstractions;
using OpenClawNet.Tools.Core;

namespace OpenClawNet.UnitTests.Tools;

public class ToolExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsFail_WhenToolNotFound()
    {
        var registry = new ToolRegistry();
        var executor = new ToolExecutor(registry, new AlwaysApprovePolicy(), NullLogger<ToolExecutor>.Instance);
        
        var result = await executor.ExecuteAsync("nonexistent", "{}");
        
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }
    
    [Fact]
    public async Task ExecuteAsync_CallsTool_WhenFound()
    {
        var registry = new ToolRegistry();
        registry.Register(new SuccessTool());
        var executor = new ToolExecutor(registry, new AlwaysApprovePolicy(), NullLogger<ToolExecutor>.Instance);
        
        var result = await executor.ExecuteAsync("success_tool", "{}");
        
        result.Success.Should().BeTrue();
        result.Output.Should().Be("executed");
    }
    
    [Fact]
    public async Task ExecuteBatchAsync_ExecutesAllTools()
    {
        var registry = new ToolRegistry();
        registry.Register(new SuccessTool());
        var executor = new ToolExecutor(registry, new AlwaysApprovePolicy(), NullLogger<ToolExecutor>.Instance);
        
        var calls = new List<(string, string)> { ("success_tool", "{}"), ("success_tool", "{}") };
        var results = await executor.ExecuteBatchAsync(calls);
        
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
    }
    
    private sealed class SuccessTool : ITool
    {
        public string Name => "success_tool";
        public string Description => "Always succeeds";
        public ToolMetadata Metadata => new()
        {
            Name = Name,
            Description = Description,
            ParameterSchema = JsonDocument.Parse("{}")
        };
        
        public Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken cancellationToken = default)
            => Task.FromResult(ToolResult.Ok(Name, "executed", TimeSpan.FromMilliseconds(1)));
    }
}
