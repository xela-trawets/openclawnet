using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using OpenClawNet.Storage;
using OpenClawNet.Storage.Entities;

namespace OpenClawNet.UnitTests.Storage;

public sealed class ModelProviderDefinitionStoreTests : IAsyncLifetime
{
    private IDbContextFactory<OpenClawDbContext> _factory = null!;

    public Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<OpenClawDbContext>()
            .UseInMemoryDatabase("test-mpd-" + Guid.NewGuid())
            .Options;
        _factory = new TestDbContextFactory(options);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SaveAsync_ThenGetAsync_ReturnsDefinition()
    {
        var store = new ModelProviderDefinitionStore(_factory);
        var definition = new ModelProviderDefinition
        {
            Name = "ollama-test",
            ProviderType = "ollama",
            DisplayName = "Ollama Test",
            Endpoint = "http://localhost:11434",
            Model = "gemma4:e2b"
        };

        await store.SaveAsync(definition);
        var result = await store.GetAsync("ollama-test");

        result.Should().NotBeNull();
        result!.Name.Should().Be("ollama-test");
        result.ProviderType.Should().Be("ollama");
        result.DisplayName.Should().Be("Ollama Test");
        result.Endpoint.Should().Be("http://localhost:11434");
        result.Model.Should().Be("gemma4:e2b");
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenNotFound()
    {
        var store = new ModelProviderDefinitionStore(_factory);

        var result = await store.GetAsync("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_ReturnsAllDefinitions_OrderedByName()
    {
        var store = new ModelProviderDefinitionStore(_factory);
        await store.SaveAsync(new ModelProviderDefinition { Name = "zeta", ProviderType = "ollama" });
        await store.SaveAsync(new ModelProviderDefinition { Name = "alpha", ProviderType = "ollama" });
        await store.SaveAsync(new ModelProviderDefinition { Name = "mid", ProviderType = "azure-openai" });

        var results = await store.ListAsync();

        results.Should().HaveCount(3);
        results[0].Name.Should().Be("alpha");
        results[1].Name.Should().Be("mid");
        results[2].Name.Should().Be("zeta");
    }

    [Fact]
    public async Task ListByTypeAsync_ReturnsOnlyMatchingType()
    {
        var store = new ModelProviderDefinitionStore(_factory);
        await store.SaveAsync(new ModelProviderDefinition { Name = "ollama-1", ProviderType = "ollama" });
        await store.SaveAsync(new ModelProviderDefinition { Name = "ollama-2", ProviderType = "ollama" });
        await store.SaveAsync(new ModelProviderDefinition { Name = "azure-1", ProviderType = "azure-openai" });

        var results = await store.ListByTypeAsync("ollama");

        results.Should().HaveCount(2);
        results.Should().AllSatisfy(d => d.ProviderType.Should().Be("ollama"));
    }

    [Fact]
    public async Task DeleteAsync_RemovesDefinition()
    {
        var store = new ModelProviderDefinitionStore(_factory);
        await store.SaveAsync(new ModelProviderDefinition { Name = "delete-me", ProviderType = "ollama" });

        await store.DeleteAsync("delete-me");

        var result = await store.GetAsync("delete-me");
        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_UpdatesExistingDefinition()
    {
        var store = new ModelProviderDefinitionStore(_factory);
        await store.SaveAsync(new ModelProviderDefinition
        {
            Name = "update-me",
            ProviderType = "ollama",
            Model = "v1"
        });

        await store.SaveAsync(new ModelProviderDefinition
        {
            Name = "update-me",
            ProviderType = "ollama",
            Model = "v2"
        });

        var result = await store.GetAsync("update-me");
        result.Should().NotBeNull();
        result!.Model.Should().Be("v2");
    }

    [Fact]
    public async Task SaveAsync_UpdatesLastTestFields_OnExisting()
    {
        var store = new ModelProviderDefinitionStore(_factory);
        await store.SaveAsync(new ModelProviderDefinition
        {
            Name = "test-fields",
            ProviderType = "ollama"
        });

        var testedAt = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        await store.SaveAsync(new ModelProviderDefinition
        {
            Name = "test-fields",
            ProviderType = "ollama",
            LastTestedAt = testedAt,
            LastTestSucceeded = true,
            LastTestError = null
        });

        var result = await store.GetAsync("test-fields");
        result.Should().NotBeNull();
        result!.LastTestedAt.Should().Be(testedAt);
        result.LastTestSucceeded.Should().BeTrue();
        result.LastTestError.Should().BeNull();

        var failedAt = testedAt.AddMinutes(5);
        await store.SaveAsync(new ModelProviderDefinition
        {
            Name = "test-fields",
            ProviderType = "ollama",
            LastTestedAt = failedAt,
            LastTestSucceeded = false,
            LastTestError = "boom"
        });

        var updated = await store.GetAsync("test-fields");
        updated!.LastTestedAt.Should().Be(failedAt);
        updated.LastTestSucceeded.Should().BeFalse();
        updated.LastTestError.Should().Be("boom");
    }

    [Fact]
    public async Task SeedDefaultsAsync_CreatesDefaults_WhenEmpty()
    {
        var store = new ModelProviderDefinitionStore(_factory);

        await store.SeedDefaultsAsync();

        var all = await store.ListAsync();
        all.Should().HaveCount(6);
    }

    [Fact]
    public async Task SeedDefaultsAsync_DoesNotDuplicate_WhenAlreadySeeded()
    {
        var store = new ModelProviderDefinitionStore(_factory);

        await store.SeedDefaultsAsync();
        await store.SeedDefaultsAsync();

        var all = await store.ListAsync();
        all.Should().HaveCount(6);
    }

    private sealed class TestDbContextFactory(DbContextOptions<OpenClawDbContext> options)
        : IDbContextFactory<OpenClawDbContext>
    {
        public OpenClawDbContext CreateDbContext() => new(options);

        public Task<OpenClawDbContext> CreateDbContextAsync(CancellationToken ct = default)
            => Task.FromResult(new OpenClawDbContext(options));
    }
}
