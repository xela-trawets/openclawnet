using FluentAssertions;
using OpenClawNet.Gateway.Services.Mcp;

namespace OpenClawNet.UnitTests.Mcp.Gateway;

public class McpSuggestionsProviderTests
{
    [Fact]
    public void Parse_RoundTripsAllFields()
    {
        const string yaml = """
        version: 1
        suggestions:
          - id: github-mcp
            name: "@modelcontextprotocol/server-github"
            description: "GitHub API access"
            transport: stdio
            command: npx
            args: ["-y", "@modelcontextprotocol/server-github"]
            category: integration
            requires_env:
              - GITHUB_PERSONAL_ACCESS_TOKEN
            homepage: https://github.com/modelcontextprotocol/servers
        """;

        var result = McpSuggestionsProvider.Parse(yaml);

        result.Should().HaveCount(1);
        var s = result[0];
        s.Id.Should().Be("github-mcp");
        s.Name.Should().Be("@modelcontextprotocol/server-github");
        s.Transport.Should().Be("stdio");
        s.Command.Should().Be("npx");
        s.Args.Should().Equal("-y", "@modelcontextprotocol/server-github");
        s.Category.Should().Be("integration");
        s.RequiresEnv.Should().Equal("GITHUB_PERSONAL_ACCESS_TOKEN");
        s.Homepage.Should().Be("https://github.com/modelcontextprotocol/servers");
    }

    [Fact]
    public void Parse_EmptyYaml_ReturnsEmpty()
    {
        var result = McpSuggestionsProvider.Parse("version: 1\n");
        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_MalformedYaml_Throws()
    {
        var act = () => McpSuggestionsProvider.Parse("not: : : valid");
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Parse_RealRepoFile_HasSixCuratedEntries()
    {
        // Locate docs/mcp-suggestions.yaml relative to the repo root, regardless of where xUnit runs.
        var dir = AppContext.BaseDirectory;
        string? repoRoot = null;
        for (var d = new DirectoryInfo(dir); d is not null; d = d.Parent)
        {
            if (File.Exists(Path.Combine(d.FullName, "OpenClawNet.slnx")))
            {
                repoRoot = d.FullName;
                break;
            }
        }
        repoRoot.Should().NotBeNull("test must run from inside the repo");

        var yamlPath = Path.Combine(repoRoot!, "docs", "mcp-suggestions.yaml");
        File.Exists(yamlPath).Should().BeTrue();

        var result = McpSuggestionsProvider.Parse(File.ReadAllText(yamlPath));
        result.Should().HaveCount(6);
        result.Select(s => s.Id).Should().Contain(new[]
        {
            "shell-alt-mkusaka", "desktop-commander", "github-mcp",
            "memory-mcp", "playwright-mcp-alt", "fetch-mcp-alt",
        });
    }
}
