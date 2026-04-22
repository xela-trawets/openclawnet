using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenClawNet.Gateway.Endpoints;
using OpenClawNet.Models.Abstractions;
using OpenClawNet.Storage;
using OpenClawNet.Storage.Entities;

namespace OpenClawNet.UnitTests.Gateway;

public sealed class ModelProviderEndpointTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task GetList_ReturnsAllProviders()
    {
        await using var app = await CreateTestAppAsync();
        using var client = app.GetTestClient();

        // Seed two providers via PUT
        await client.PutAsJsonAsync("/api/model-providers/ollama-1", new
        {
            providerType = "ollama",
            displayName = "Ollama 1",
            endpoint = "http://localhost:11434",
            model = "gemma4:e2b"
        });
        await client.PutAsJsonAsync("/api/model-providers/azure-1", new
        {
            providerType = "azure-openai",
            displayName = "Azure 1",
            model = "gpt-4o"
        });

        var response = await client.GetAsync("/api/model-providers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var providers = await response.Content.ReadFromJsonAsync<List<ModelProviderResponse>>(JsonOpts);
        providers.Should().NotBeNull();
        providers!.Count.Should().BeGreaterThanOrEqualTo(2);
        providers.Select(p => p.Name).Should().Contain("ollama-1").And.Contain("azure-1");
    }

    [Fact]
    public async Task GetByName_ExistingProvider_ReturnsOk()
    {
        await using var app = await CreateTestAppAsync();
        using var client = app.GetTestClient();

        await client.PutAsJsonAsync("/api/model-providers/lookup-test", new
        {
            providerType = "ollama",
            displayName = "Lookup Test",
            model = "llama3"
        });

        var response = await client.GetAsync("/api/model-providers/lookup-test");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var provider = await response.Content.ReadFromJsonAsync<ModelProviderResponse>(JsonOpts);
        provider!.Name.Should().Be("lookup-test");
        provider.ProviderType.Should().Be("ollama");
    }

    [Fact]
    public async Task GetByName_NonExistent_ReturnsNotFound()
    {
        await using var app = await CreateTestAppAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/api/model-providers/no-such-provider");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PutProvider_CreatesNewProvider()
    {
        await using var app = await CreateTestAppAsync();
        using var client = app.GetTestClient();

        var response = await client.PutAsJsonAsync("/api/model-providers/my-ollama", new
        {
            providerType = "ollama",
            displayName = "My Ollama",
            endpoint = "http://localhost:11434",
            model = "gemma4:e2b"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var provider = await response.Content.ReadFromJsonAsync<ModelProviderResponse>(JsonOpts);
        provider.Should().NotBeNull();
        provider!.Name.Should().Be("my-ollama");
        provider.ProviderType.Should().Be("ollama");
        provider.Model.Should().Be("gemma4:e2b");
    }

    [Fact]
    public async Task PutProvider_UpdatesExistingProvider()
    {
        await using var app = await CreateTestAppAsync();
        using var client = app.GetTestClient();

        // Create
        await client.PutAsJsonAsync("/api/model-providers/updatable", new
        {
            providerType = "ollama",
            displayName = "Original",
            model = "v1"
        });

        // Update
        var response = await client.PutAsJsonAsync("/api/model-providers/updatable", new
        {
            providerType = "ollama",
            displayName = "Updated",
            model = "v2"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var provider = await response.Content.ReadFromJsonAsync<ModelProviderResponse>(JsonOpts);
        provider!.DisplayName.Should().Be("Updated");
        provider.Model.Should().Be("v2");
    }

    [Fact]
    public async Task PutProvider_PreservesApiKey_WhenNotProvided()
    {
        await using var app = await CreateTestAppAsync();
        using var client = app.GetTestClient();

        // Create with API key
        await client.PutAsJsonAsync("/api/model-providers/keyed", new
        {
            providerType = "azure-openai",
            displayName = "Azure Keyed",
            model = "gpt-4o",
            apiKey = "secret-key-123"
        });

        // Update without API key
        await client.PutAsJsonAsync("/api/model-providers/keyed", new
        {
            providerType = "azure-openai",
            displayName = "Azure Keyed Updated",
            model = "gpt-4o-mini"
        });

        // Verify key preserved by checking hasApiKey in response
        var response = await client.GetAsync("/api/model-providers/keyed");
        var provider = await response.Content.ReadFromJsonAsync<ModelProviderResponse>(JsonOpts);
        provider!.HasApiKey.Should().BeTrue("API key should be preserved when not provided in update");
        provider.DisplayName.Should().Be("Azure Keyed Updated");
    }

    [Fact]
    public async Task DeleteProvider_RemovesProvider()
    {
        await using var app = await CreateTestAppAsync();
        using var client = app.GetTestClient();

        await client.PutAsJsonAsync("/api/model-providers/deletable", new
        {
            providerType = "ollama",
            displayName = "Delete Me",
            model = "llama3"
        });

        var deleteResponse = await client.DeleteAsync("/api/model-providers/deletable");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync("/api/model-providers/deletable");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteProvider_NonExistent_ReturnsNoContent()
    {
        await using var app = await CreateTestAppAsync();
        using var client = app.GetTestClient();

        var response = await client.DeleteAsync("/api/model-providers/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetList_MasksApiKey_ReturnsHasApiKeyFlag()
    {
        await using var app = await CreateTestAppAsync();
        using var client = app.GetTestClient();

        await client.PutAsJsonAsync("/api/model-providers/secret-provider", new
        {
            providerType = "azure-openai",
            displayName = "Secret Provider",
            model = "gpt-4o",
            apiKey = "super-secret-key"
        });

        var response = await client.GetAsync("/api/model-providers");
        var body = await response.Content.ReadAsStringAsync();

        // The response should contain hasApiKey=true but NOT the actual key
        body.Should().NotContain("super-secret-key");

        var providers = JsonSerializer.Deserialize<List<ModelProviderResponse>>(body, JsonOpts);
        var secretProvider = providers!.First(p => p.Name == "secret-provider");
        secretProvider.HasApiKey.Should().BeTrue();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<WebApplication> CreateTestAppAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseTestServer();

        builder.Services.AddDbContextFactory<OpenClawDbContext>(o =>
            o.UseInMemoryDatabase("test-mpe-" + Guid.NewGuid()));
        builder.Services.AddScoped<IModelProviderDefinitionStore, ModelProviderDefinitionStore>();

        // Mock IAgentProvider for test endpoint (not testing actual connectivity)
        var mockProvider = new Mock<IAgentProvider>();
        mockProvider.Setup(p => p.ProviderName).Returns("ollama");
        mockProvider.Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        builder.Services.AddSingleton<IAgentProvider>(mockProvider.Object);

        var app = builder.Build();
        app.MapModelProviderEndpoints();
        await app.StartAsync();
        return app;
    }
}
