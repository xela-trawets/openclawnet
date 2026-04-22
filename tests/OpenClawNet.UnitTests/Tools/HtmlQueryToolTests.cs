using Microsoft.Extensions.Logging.Abstractions;
using OpenClawNet.Tools.Abstractions;
using OpenClawNet.Tools.HtmlQuery;
using Xunit;

namespace OpenClawNet.UnitTests.Tools;

public class HtmlQueryToolTests
{
    private static ToolInput Args(string json) => new() { ToolName = "html_query", RawArguments = json };

    [Fact]
    public async Task Missing_Url_Fails()
    {
        var tool = new HtmlQueryTool(NullLogger<HtmlQueryTool>.Instance);
        var result = await tool.ExecuteAsync(Args("""{ "selector": "h1" }"""));
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Local_Url_Is_Rejected()
    {
        var tool = new HtmlQueryTool(NullLogger<HtmlQueryTool>.Instance);
        var result = await tool.ExecuteAsync(Args("""{ "url": "http://localhost:1234", "selector": "h1" }"""));
        Assert.False(result.Success);
        Assert.Contains("local", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Invalid_Url_Is_Rejected()
    {
        var tool = new HtmlQueryTool(NullLogger<HtmlQueryTool>.Instance);
        var result = await tool.ExecuteAsync(Args("""{ "url": "ftp://example.com", "selector": "h1" }"""));
        Assert.False(result.Success);
    }
}
