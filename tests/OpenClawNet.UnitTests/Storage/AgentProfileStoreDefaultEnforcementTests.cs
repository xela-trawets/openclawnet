using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OpenClawNet.Models.Abstractions;
using OpenClawNet.Storage;

namespace OpenClawNet.UnitTests.Storage;

/// <summary>
/// Tests for the single-default enforcement logic added in Phase 2.
/// When <see cref="AgentProfileStore.SaveAsync"/> saves a profile with
/// <see cref="AgentProfile.IsDefault"/> = true, all other profiles must
/// have their IsDefault cleared.
/// </summary>
public class AgentProfileStoreDefaultEnforcementTests : IDisposable
{
    private readonly IDbContextFactory<OpenClawDbContext> _factory;

    public AgentProfileStoreDefaultEnforcementTests()
    {
        var options = new DbContextOptionsBuilder<OpenClawDbContext>()
            .UseInMemoryDatabase(databaseName: "default-enforcement-" + Guid.NewGuid())
            .Options;
        _factory = new TestDbContextFactory(options);
    }

    [Fact]
    public async Task SaveAsync_WithIsDefaultTrue_ClearsOtherDefaults()
    {
        var store = new AgentProfileStore(_factory);

        // Arrange: save two profiles, both marked as default
        await store.SaveAsync(new AgentProfile { Name = "profile-a", IsDefault = true });
        await store.SaveAsync(new AgentProfile { Name = "profile-b", IsDefault = true });

        // Act: verify that only profile-b is now default
        var a = await store.GetAsync("profile-a");
        var b = await store.GetAsync("profile-b");

        a!.IsDefault.Should().BeFalse("saving profile-b as default should clear profile-a's default flag");
        b!.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_WithIsDefaultTrue_OnlyOneDefaultRemains()
    {
        var store = new AgentProfileStore(_factory);

        await store.SaveAsync(new AgentProfile { Name = "first", IsDefault = true });
        await store.SaveAsync(new AgentProfile { Name = "second", IsDefault = true });
        await store.SaveAsync(new AgentProfile { Name = "third", IsDefault = true });

        var all = await store.ListAsync();
        var defaults = all.Where(p => p.IsDefault).ToList();

        defaults.Should().ContainSingle("only the last-saved default should remain");
        defaults[0].Name.Should().Be("third");
    }

    [Fact]
    public async Task SaveAsync_WithIsDefaultFalse_DoesNotAffectExistingDefault()
    {
        var store = new AgentProfileStore(_factory);

        await store.SaveAsync(new AgentProfile { Name = "the-default", IsDefault = true });
        await store.SaveAsync(new AgentProfile { Name = "non-default", IsDefault = false });

        var theDefault = await store.GetAsync("the-default");
        theDefault!.IsDefault.Should().BeTrue("saving a non-default profile should not clear existing default");
    }

    [Fact]
    public async Task SaveAsync_UpdateExistingToDefault_ClearsOtherDefaults()
    {
        var store = new AgentProfileStore(_factory);

        await store.SaveAsync(new AgentProfile { Name = "alpha", IsDefault = true });
        await store.SaveAsync(new AgentProfile { Name = "beta", IsDefault = false });

        // Now update beta to be the new default
        await store.SaveAsync(new AgentProfile { Name = "beta", IsDefault = true });

        var alpha = await store.GetAsync("alpha");
        var beta = await store.GetAsync("beta");

        alpha!.IsDefault.Should().BeFalse();
        beta!.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task GetDefaultAsync_SeedsDefault_WhenNoProfilesExist()
    {
        var store = new AgentProfileStore(_factory);

        var result = await store.GetDefaultAsync();

        result.Should().NotBeNull();
        result.Name.Should().Be("openclawnet-agent");
        result.IsDefault.Should().BeTrue();
        result.Instructions.Should().NotBeNullOrWhiteSpace();

        // Verify it was persisted
        var persisted = await store.GetAsync("openclawnet-agent");
        persisted.Should().NotBeNull();
    }

    [Fact]
    public async Task GetDefaultAsync_ReturnsExistingDefault_DoesNotSeedNew()
    {
        var store = new AgentProfileStore(_factory);

        await store.SaveAsync(new AgentProfile
        {
            Name = "custom-default",
            IsDefault = true,
            Instructions = "Custom instructions"
        });

        var result = await store.GetDefaultAsync();

        result.Name.Should().Be("custom-default");

        // No extra "default" profile should have been created
        var all = await store.ListAsync();
        all.Should().ContainSingle();
    }

    [Fact]
    public async Task GetDefaultAsync_SeededDefault_HasOllamaDefaultProvider()
    {
        var store = new AgentProfileStore(_factory);
        var result = await store.GetDefaultAsync();
        result.Provider.Should().Be("ollama-default");
    }

    public void Dispose()
    {
        // InMemory databases are automatically cleaned up
    }

    private sealed class TestDbContextFactory : IDbContextFactory<OpenClawDbContext>
    {
        private readonly DbContextOptions<OpenClawDbContext> _options;
        public TestDbContextFactory(DbContextOptions<OpenClawDbContext> options) => _options = options;
        public OpenClawDbContext CreateDbContext() => new(_options);
    }
}
