using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenClawNet.Gateway.Services;
using OpenClawNet.Models.Abstractions;

namespace OpenClawNet.UnitTests.Services;

public class RuntimeAgentProviderTests
{
    [Fact]
    public void CreateChatClient_RoutesToCorrectProvider_BasedOnSettings()
    {
        var ollamaProvider = CreateMockProvider("ollama");
        var azureProvider = CreateMockProvider("azure-openai");
        var settings = CreateSettings("ollama");

        var runtime = new RuntimeAgentProvider(
            BuildServiceProvider(ollamaProvider.Object, azureProvider.Object),
            settings,
            NullLogger<RuntimeAgentProvider>.Instance);

        var profile = new AgentProfile { Name = "test" };
        runtime.CreateChatClient(profile);

        ollamaProvider.Verify(p => p.CreateChatClient(profile), Times.Once);
        azureProvider.Verify(p => p.CreateChatClient(profile), Times.Never);
    }

    [Fact]
    public void CreateChatClient_UsesProfileProvider_WhenSpecified()
    {
        var ollamaProvider = CreateMockProvider("ollama");
        var azureProvider = CreateMockProvider("azure-openai");
        var settings = CreateSettings("ollama");

        var runtime = new RuntimeAgentProvider(
            BuildServiceProvider(ollamaProvider.Object, azureProvider.Object),
            settings,
            NullLogger<RuntimeAgentProvider>.Instance);

        var profile = new AgentProfile { Name = "test", Provider = "azure-openai" };
        runtime.CreateChatClient(profile);

        azureProvider.Verify(p => p.CreateChatClient(profile), Times.Once);
        ollamaProvider.Verify(p => p.CreateChatClient(profile), Times.Never);
    }

    [Fact]
    public void CreateChatClient_Throws_WhenUnknownProviderName()
    {
        var ollamaProvider = CreateMockProvider("ollama");
        var settings = CreateSettings("ollama");

        var runtime = new RuntimeAgentProvider(
            BuildServiceProvider(ollamaProvider.Object),
            settings,
            NullLogger<RuntimeAgentProvider>.Instance);

        var profile = new AgentProfile { Name = "test", Provider = "unknown" };

        var act = () => runtime.CreateChatClient(profile);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No agent provider registered*'unknown'*");
    }

    [Fact]
    public async Task IsAvailableAsync_DelegatesToActiveProvider()
    {
        var provider = CreateMockProvider("ollama");
        provider.Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var settings = CreateSettings("ollama");

        var runtime = new RuntimeAgentProvider(
            BuildServiceProvider(provider.Object),
            settings,
            NullLogger<RuntimeAgentProvider>.Instance);

        var result = await runtime.IsAvailableAsync();

        result.Should().BeTrue();
        provider.Verify(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ProviderName_ReturnsActiveProvidersName()
    {
        var provider = CreateMockProvider("ollama");
        var settings = CreateSettings("ollama");

        var runtime = new RuntimeAgentProvider(
            BuildServiceProvider(provider.Object),
            settings,
            NullLogger<RuntimeAgentProvider>.Instance);

        runtime.ProviderName.Should().Be("ollama");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IServiceProvider BuildServiceProvider(params IAgentProvider[] providers)
    {
        var services = new ServiceCollection();
        foreach (var p in providers)
            services.AddSingleton(p);
        return services.BuildServiceProvider();
    }

    private static Mock<IAgentProvider> CreateMockProvider(string name)
    {
        var mock = new Mock<IAgentProvider>();
        mock.SetupGet(p => p.ProviderName).Returns(name);
        mock.Setup(p => p.CreateChatClient(It.IsAny<AgentProfile>()))
            .Returns(new Mock<IChatClient>().Object);
        return mock;
    }

    private static RuntimeModelSettings CreateSettings(string providerName)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Model:Provider"] = providerName
            })
            .Build();

        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath)
            .Returns(Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString()));

        return new RuntimeModelSettings(config, env.Object, NullLogger<RuntimeModelSettings>.Instance);
    }
}
