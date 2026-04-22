using Microsoft.Extensions.Logging.Abstractions;
using OpenClawNet.Tools.Abstractions;
using OpenClawNet.Tools.Calculator;
using Xunit;

namespace OpenClawNet.UnitTests.Tools;

public class CalculatorToolTests
{
    private static CalculatorTool CreateTool() => new(NullLogger<CalculatorTool>.Instance);

    private static ToolInput Args(string json) => new()
    {
        ToolName = "calculator",
        RawArguments = json
    };

    [Theory]
    [InlineData("2 + 3", "5")]
    [InlineData("10 / 4", "2.5")]
    [InlineData("Pow(2, 10)", "1024")]
    [InlineData("Sqrt(16)", "4")]
    [InlineData("Max(7, 3)", "7")]
    [InlineData("if(1 > 2, \\\"a\\\", \\\"b\\\")", "b")]
    public async Task Evaluate_Returns_Expected(string expression, string expected)
    {
        var tool = CreateTool();
        var result = await tool.ExecuteAsync(Args($"{{\"expression\":\"{expression}\"}}"));
        Assert.True(result.Success, result.Error);
        Assert.Contains(expected, result.Output);
    }

    [Fact]
    public async Task Missing_Expression_Fails()
    {
        var result = await CreateTool().ExecuteAsync(Args("{}"));
        Assert.False(result.Success);
        Assert.Contains("expression", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Invalid_Expression_Returns_Error()
    {
        var result = await CreateTool().ExecuteAsync(Args("{\"expression\":\"2 +\"}"));
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Metadata_Has_Required_Schema_Field()
    {
        var meta = CreateTool().Metadata;
        Assert.Equal("calculator", meta.Name);
        var root = meta.ParameterSchema.RootElement;
        var required = root.GetProperty("required");
        Assert.Equal("expression", required[0].GetString());
    }
}
