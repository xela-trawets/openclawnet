using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenClawNet.Models.Abstractions;
using OpenClawNet.Models.AzureOpenAI;

namespace OpenClawNet.UnitTests.Models;

/// <summary>
/// Tests for AzureOpenAIModelClient configuration, auth mode selection, and no-config behavior.
/// No real Azure endpoint is called — these tests validate DI wiring, option binding,
/// and graceful behavior when credentials are missing.
/// </summary>
public sealed class AzureOpenAIModelClientTests
{
    // ── ProviderName ──────────────────────────────────────────────────────────

    [Fact]
    public void ProviderName_IsAzureOpenAI()
    {
        var client = BuildClient(new AzureOpenAIOptions
        {
            Endpoint = "https://my-resource.openai.azure.com/",
            ApiKey = "test-key"
        });
        client.ProviderName.Should().Be("azure-openai");
    }

    // ── No-config behavior ────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ThrowsInvalidOperation_WhenEndpointNotConfigured()
    {
        var act = () => BuildClient(new AzureOpenAIOptions()); // empty endpoint
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*endpoint not configured*");
    }

    [Fact]
    public async Task CompleteAsync_Throws_WhenNotConfigured()
    {
        var act = () => BuildClient(new AzureOpenAIOptions());
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not configured*");
    }

    [Fact]
    public async Task StreamAsync_Throws_WhenNotConfigured()
    {
        var act = () => BuildClient(new AzureOpenAIOptions());
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not configured*");
    }

    // ── API key auth ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_DoesNotThrow_WhenApiKeyModeFullyConfigured()
    {
        var act = () => BuildClient(new AzureOpenAIOptions
        {
            Endpoint = "https://my-resource.openai.azure.com/",
            ApiKey = "sk-test-key",
            DeploymentName = "gpt-5-mini",
            AuthMode = "api-key"
        });
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_ThrowsInvalidOperation_WhenApiKeyMissingInApiKeyMode()
    {
        // Missing ApiKey in api-key mode → constructor throws (fail fast)
        var act = () => BuildClient(new AzureOpenAIOptions
        {
            Endpoint = "https://my-resource.openai.azure.com/",
            ApiKey = null, // missing
            AuthMode = "api-key"
        });
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*API key not configured*");
    }

    // ── Integrated auth ───────────────────────────────────────────────────────

    [Fact]
    public void Constructor_DoesNotThrow_WhenIntegratedModeWithEndpointOnly()
    {
        // DefaultAzureCredential instantiation should not throw (auth happens lazily)
        var act = () => BuildClient(new AzureOpenAIOptions
        {
            Endpoint = "https://my-resource.openai.azure.com/",
            AuthMode = "integrated"
            // No ApiKey needed
        });
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_ThrowsInvalidOperation_WhenIntegratedModeButNoEndpoint()
    {
        var act = () => BuildClient(new AzureOpenAIOptions
        {
            Endpoint = string.Empty,
            AuthMode = "integrated"
        });
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*endpoint not configured*");
    }

    // ── Auth mode case insensitivity ──────────────────────────────────────────

    [Theory]
    [InlineData("integrated")]
    [InlineData("Integrated")]
    [InlineData("INTEGRATED")]
    public void Constructor_HandlesIntegratedAuthMode_CaseInsensitively(string authMode)
    {
        var act = () => BuildClient(new AzureOpenAIOptions
        {
            Endpoint = "https://my-resource.openai.azure.com/",
            AuthMode = authMode
        });
        act.Should().NotThrow();
    }

    // ── Default options ───────────────────────────────────────────────────────

    [Fact]
    public void DefaultOptions_HaveGpt5MiniDeployment()
    {
        var opts = new AzureOpenAIOptions();
        opts.DeploymentName.Should().Be("gpt-5-mini");
    }

    [Fact]
    public void DefaultOptions_UseApiKeyAuthMode()
    {
        var opts = new AzureOpenAIOptions();
        opts.AuthMode.Should().Be("api-key");
    }

    // ── DI / options binding ──────────────────────────────────────────────────

    [Fact]
    public void AddAzureOpenAI_RegistersIModelClient()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Model:Endpoint"] = "https://my-resource.openai.azure.com/",
                ["Model:ApiKey"] = "test-key",
                ["Model:DeploymentName"] = "gpt-5-mini"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddAzureOpenAI();

        var provider = services.BuildServiceProvider();
        var client = provider.GetService<IModelClient>();
        client.Should().NotBeNull();
        client.Should().BeOfType<AzureOpenAIModelClient>();
    }

    [Fact]
    public void AddAzureOpenAI_ReadsEndpointFromModelSection()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Model:Endpoint"] = "https://test.openai.azure.com/",
                ["Model:ApiKey"] = "test-key",
                ["Model:DeploymentName"] = "gpt-5-mini",
                ["Model:AuthMode"] = "api-key"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddAzureOpenAI();

        var provider = services.BuildServiceProvider();
        var opts = provider.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value;

        opts.Endpoint.Should().Be("https://test.openai.azure.com/");
        opts.ApiKey.Should().Be("test-key");
        opts.DeploymentName.Should().Be("gpt-5-mini");
        opts.AuthMode.Should().Be("api-key");
    }

    [Fact]
    public void AddAzureOpenAI_ReadsEndpointFromAzureOpenAISection_TakingPrecedence()
    {
        // AzureOpenAI:* keys should override Model:* keys
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Model:Endpoint"] = "https://old.openai.azure.com/",
                ["Model:ApiKey"] = "old-key",
                ["AzureOpenAI:Endpoint"] = "https://new.openai.azure.com/",
                ["AzureOpenAI:ApiKey"] = "new-key",
                ["AzureOpenAI:AuthMode"] = "integrated"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddAzureOpenAI();

        var provider = services.BuildServiceProvider();
        var opts = provider.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value;

        opts.Endpoint.Should().Be("https://new.openai.azure.com/");
        opts.ApiKey.Should().Be("new-key");
        opts.AuthMode.Should().Be("integrated");
    }

    [Fact]
    public void AddAzureOpenAI_DefaultDeploymentName_IsGpt5Mini()
    {
        var config = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddAzureOpenAI();

        var provider = services.BuildServiceProvider();
        var opts = provider.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value;

        opts.DeploymentName.Should().Be("gpt-5-mini");
    }

    // ── Model fallback ────────────────────────────────────────────────────────

    [Fact]
    public void Options_EmptyDeploymentKeepsDefault_WhenNotSetInConfig()
    {
        // When no config is provided for DeploymentName, it should keep "gpt-5-mini"
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Model:Endpoint"] = "https://test.openai.azure.com/",
                ["Model:ApiKey"] = "test-key"
                // no DeploymentName
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddAzureOpenAI();

        var provider = services.BuildServiceProvider();
        var opts = provider.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value;

        opts.DeploymentName.Should().Be("gpt-5-mini");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AzureOpenAIModelClient BuildClient(AzureOpenAIOptions options)
    {
        var opts = Options.Create(options);
        return new AzureOpenAIModelClient(opts, NullLogger<AzureOpenAIModelClient>.Instance);
    }
}
