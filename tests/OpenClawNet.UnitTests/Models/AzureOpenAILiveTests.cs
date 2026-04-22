using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenClawNet.Models.Abstractions;
using OpenClawNet.Models.AzureOpenAI;

namespace OpenClawNet.UnitTests.Models;

/// <summary>
/// Live tests that call the real Azure OpenAI endpoint.
/// Requires user secrets set on the Gateway project:
///   dotnet user-secrets set "Model:Provider" "azure-openai" --project src\OpenClawNet.Gateway
///   dotnet user-secrets set "Model:Endpoint" "https://YOUR.openai.azure.com/" --project src\OpenClawNet.Gateway
///   dotnet user-secrets set "Model:ApiKey" "YOUR-KEY" --project src\OpenClawNet.Gateway
///   dotnet user-secrets set "Model:DeploymentName" "gpt-5-mini" --project src\OpenClawNet.Gateway
///
/// Run with: dotnet test --filter "Category=Live"
/// Skipped automatically when credentials are not configured.
/// </summary>
[Trait("Category", "Live")]
public sealed class AzureOpenAILiveTests
{
    // Gateway's UserSecretsId — set by `dotnet user-secrets init`
    private const string GatewayUserSecretsId = "c15754a6-dc90-4a2a-aecb-1233d1a54fe1";

    private readonly AzureOpenAIOptions _options;
    private readonly bool _isConfigured;

    public AzureOpenAILiveTests()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets(GatewayUserSecretsId, reloadOnChange: false)
            .Build();

        _options = new AzureOpenAIOptions();
        if (config["Model:Endpoint"] is { Length: > 0 } ep)       _options.Endpoint = ep;
        if (config["Model:ApiKey"] is { Length: > 0 } key)         _options.ApiKey = key;
        if (config["Model:DeploymentName"] is { Length: > 0 } dep) _options.DeploymentName = dep;
        if (config["Model:AuthMode"] is { Length: > 0 } mode)      _options.AuthMode = mode;

        _isConfigured = !string.IsNullOrEmpty(_options.Endpoint)
            && (_options.AuthMode.Equals("integrated", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrEmpty(_options.ApiKey));
    }

    [SkippableFact]
    public async Task CompleteAsync_ReturnsNonEmptyResponse()
    {
        Skip.If(!_isConfigured, "Azure OpenAI credentials not configured — set user secrets to run live tests.");

        var client = BuildClient();
        var response = await client.CompleteAsync(new ChatRequest
        {
            Messages =
            [
                new ChatMessage { Role = ChatMessageRole.System,  Content = "You are a helpful assistant. Be brief." },
                new ChatMessage { Role = ChatMessageRole.User,    Content = "Say hello in exactly one sentence." }
            ]
        });

        response.Content.Should().NotBeNullOrWhiteSpace("the model should return a greeting");
        response.Role.Should().Be(ChatMessageRole.Assistant);
        response.Model.Should().NotBeNullOrWhiteSpace();
        response.Usage.Should().NotBeNull();
        response.Usage!.TotalTokens.Should().BeGreaterThan(0);
    }

    [SkippableFact]
    public async Task StreamAsync_YieldsChunksAndCompletesSuccessfully()
    {
        Skip.If(!_isConfigured, "Azure OpenAI credentials not configured — set user secrets to run live tests.");

        var client = BuildClient();
        var chunks = new List<ChatResponseChunk>();

        await foreach (var chunk in client.StreamAsync(new ChatRequest
        {
            Messages =
            [
                new ChatMessage { Role = ChatMessageRole.System, Content = "You are a helpful assistant. Be brief." },
                new ChatMessage { Role = ChatMessageRole.User,   Content = "Count from 1 to 3." }
            ]
        }))
        {
            chunks.Add(chunk);
        }

        chunks.Should().NotBeEmpty("streaming should yield at least one chunk");
        var fullContent = string.Concat(chunks.Select(c => c.Content ?? ""));
        fullContent.Should().NotBeNullOrWhiteSpace("streamed chunks should contain text");
    }

    [SkippableFact]
    public async Task IsAvailableAsync_ReturnsTrueWhenConfigured()
    {
        Skip.If(!_isConfigured, "Azure OpenAI credentials not configured — set user secrets to run live tests.");

        var client = BuildClient();
        var available = await client.IsAvailableAsync();
        available.Should().BeTrue("endpoint should be reachable with valid credentials");
    }

    [SkippableFact]
    public async Task StreamAsync_WithTools_YieldsToolCallChunk()
    {
        Skip.If(!_isConfigured, "Azure OpenAI credentials not configured — set user secrets to run live tests.");

        var client = BuildClient();
        var request = new ChatRequest
        {
            Messages =
            [
                new ChatMessage
                {
                    Role = ChatMessageRole.System,
                    Content = "You are a helpful assistant. When the user asks to list files, always use the list_files tool."
                },
                new ChatMessage
                {
                    Role = ChatMessageRole.User,
                    Content = "List the files in /src"
                }
            ],
            Tools =
            [
                new ToolDefinition
                {
                    Name = "list_files",
                    Description = "Lists files and directories at the given path",
                    Parameters = JsonDocument.Parse("""
                        {
                            "type": "object",
                            "properties": {
                                "path": {
                                    "type": "string",
                                    "description": "The directory path to list"
                                }
                            },
                            "required": ["path"]
                        }
                        """)
                }
            ]
        };

        var chunks = new List<ChatResponseChunk>();
        await foreach (var chunk in client.StreamAsync(request))
        {
            chunks.Add(chunk);
        }

        chunks.Should().NotBeEmpty("streaming should yield at least one chunk");

        var toolCallChunks = chunks.Where(c => c.ToolCalls is { Count: > 0 }).ToList();
        toolCallChunks.Should().NotBeEmpty(
            "when tools are provided and the prompt asks to use them, the model should return at least one tool call");

        var firstToolCall = toolCallChunks[0].ToolCalls![0];
        firstToolCall.Name.Should().Be("list_files");
        firstToolCall.Id.Should().NotBeNullOrWhiteSpace();
        firstToolCall.Arguments.Should().Contain("path");
    }

    private AzureOpenAIModelClient BuildClient() =>
        new(Options.Create(_options), NullLogger<AzureOpenAIModelClient>.Instance);
}
