using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using OpenClawNet.Mcp.Browser;
using OpenClawNet.Mcp.FileSystem;
using OpenClawNet.Mcp.Shell;
using OpenClawNet.Mcp.Web;
using OpenClawNet.Tools.Browser;
using OpenClawNet.Tools.FileSystem;
using OpenClawNet.Tools.Shell;
using OpenClawNet.Tools.Web;

namespace OpenClawNet.UnitTests.Mcp;

public class BundledMcpWrapperTests
{
    private static IEnumerable<MethodInfo> ToolMethods(Type t) =>
        t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null);

    // ------------------ Web ------------------

    [Fact]
    public void WebMcpTools_ExposesSingleFetchTool()
    {
        var methods = ToolMethods(typeof(WebMcpTools)).ToList();
        methods.Should().HaveCount(1);
        methods.Single().GetCustomAttribute<McpServerToolAttribute>()!.Name.Should().Be("fetch");
    }

    [Fact]
    public async Task WebMcpTools_Fetch_ReturnsHttpContent()
    {
        var handler = new StubHttpHandler((req, ct) =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("hello world"),
            });
        });
        var client = new HttpClient(handler);
        var tool = new WebTool(client, NullLogger<WebTool>.Instance, Options.Create(new WebToolOptions()));
        var wrapper = new WebMcpTools(tool);

        var result = await wrapper.FetchAsync("https://example.com");

        result.Should().Contain("hello world");
        result.Should().Contain("HTTP 200");
    }

    [Fact]
    public void WebBundledMcp_DefinitionShape()
    {
        var reg = new WebBundledMcp();
        reg.Definition.Name.Should().Be("web");
        reg.Definition.Transport.ToString().Should().Be("InProcess");
        reg.Definition.Enabled.Should().BeTrue();
        reg.Definition.IsBuiltIn.Should().BeTrue();
    }

    // ------------------ Shell ------------------

    [Fact]
    public void ShellMcpTools_ExposesSingleExecTool()
    {
        var methods = ToolMethods(typeof(ShellMcpTools)).ToList();
        methods.Should().HaveCount(1);
        methods.Single().GetCustomAttribute<McpServerToolAttribute>()!.Name.Should().Be("exec");
    }

    [Fact]
    public async Task ShellMcpTools_Exec_DelegatesToShellService()
    {
        var handler = new StubHttpHandler((req, ct) =>
        {
            req.RequestUri!.AbsolutePath.Should().Be("/api/shell/execute");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    success = true,
                    exitCode = 0,
                    stdout = "echo: hi",
                    stderr = "",
                    timedOut = false,
                }),
            });
        });
        var factory = new SingleHttpClientFactory("shell-service", new HttpClient(handler)
        {
            BaseAddress = new Uri("http://shell-service.local"),
        });
        var tool = new ShellTool(factory, NullLogger<ShellTool>.Instance);
        var wrapper = new ShellMcpTools(tool);

        var result = await wrapper.ExecAsync("echo hi");

        result.Should().Contain("echo: hi");
    }

    // ------------------ Browser ------------------

    [Fact]
    public void BrowserMcpTools_ExposesFiveActionTools()
    {
        var names = ToolMethods(typeof(BrowserMcpTools))
            .Select(m => m.GetCustomAttribute<McpServerToolAttribute>()!.Name)
            .OrderBy(n => n)
            .ToArray();

        names.Should().BeEquivalentTo(["click", "extract_text", "fill", "navigate", "screenshot"]);
    }

    [Fact]
    public async Task BrowserMcpTools_Navigate_ForwardsActionToBrowserService()
    {
        string? capturedAction = null;
        var handler = new StubHttpHandler(async (req, ct) =>
        {
            var json = await req.Content!.ReadAsStringAsync(ct);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            capturedAction = doc.RootElement.GetProperty("action").GetString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { success = true, output = "navigated" }),
            };
        });
        var factory = new SingleHttpClientFactory("browser-service", new HttpClient(handler)
        {
            BaseAddress = new Uri("http://browser.local"),
        });
        var tool = new BrowserTool(factory, NullLogger<BrowserTool>.Instance);
        var wrapper = new BrowserMcpTools(tool);

        var result = await wrapper.NavigateAsync("https://example.com");

        capturedAction.Should().Be("navigate");
        result.Should().Be("navigated");
    }

    // ------------------ FileSystem ------------------

    [Fact]
    public void FileSystemMcpTools_ExposesFourActionTools()
    {
        var names = ToolMethods(typeof(FileSystemMcpTools))
            .Select(m => m.GetCustomAttribute<McpServerToolAttribute>()!.Name)
            .OrderBy(n => n)
            .ToArray();

        names.Should().BeEquivalentTo(["find_projects", "list", "read", "write"]);
    }

    [Fact]
    public async Task FileSystemMcpTools_Read_ReturnsFileContents()
    {
        var tempDir = Directory.CreateTempSubdirectory("fs-mcp-test");
        try
        {
            var file = Path.Combine(tempDir.FullName, "hello.txt");
            await File.WriteAllTextAsync(file, "the contents");

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Agent:WorkspacePath"] = tempDir.FullName,
                })
                .Build();
            var tool = new FileSystemTool(NullLogger<FileSystemTool>.Instance, config);
            var wrapper = new FileSystemMcpTools(tool);

            var result = await wrapper.ReadAsync("hello.txt");

            result.Should().Be("the contents");
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    // ------------------ Helpers ------------------

    private sealed class StubHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;
        public StubHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) => _send = send;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _send(request, cancellationToken);
    }

    private sealed class SingleHttpClientFactory : IHttpClientFactory
    {
        private readonly string _name;
        private readonly HttpClient _client;
        public SingleHttpClientFactory(string name, HttpClient client) { _name = name; _client = client; }
        public HttpClient CreateClient(string name) => name == _name ? _client : throw new InvalidOperationException($"Unexpected client name: {name}");
    }
}
