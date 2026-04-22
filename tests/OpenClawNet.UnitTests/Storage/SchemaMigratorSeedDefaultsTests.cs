using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OpenClawNet.Storage;
using OpenClawNet.Storage.Entities;

namespace OpenClawNet.UnitTests.Storage;

/// <summary>
/// Covers PR-E's destructive seed step: wipe + re-create the 4 bundled MCP rows,
/// remap legacy <c>EnabledTools</c> bare names, and never run twice.
/// </summary>
public class SchemaMigratorSeedDefaultsTests
{
    [Fact]
    public async Task Seed_CreatesFourBuiltInsOnEmptyDb()
    {
        await using var db = NewDb();

        await SchemaMigrator.SeedDefaultMcpServersAsync(db);

        var rows = await db.McpServerDefinitions.ToListAsync();
        rows.Should().HaveCount(4);
        rows.Select(r => r.Name).Should().BeEquivalentTo(new[] { "web", "shell", "browser", "filesystem" });
        rows.Should().OnlyContain(r => r.IsBuiltIn && r.Enabled && r.Transport == "InProcess");
    }

    [Fact]
    public async Task Seed_IsIdempotent()
    {
        await using var db = NewDb();

        await SchemaMigrator.SeedDefaultMcpServersAsync(db);
        // Mutate after first run to prove the second invocation does NOT wipe.
        var web = await db.McpServerDefinitions.FirstAsync(r => r.Name == "web");
        web.Enabled = false;
        await db.SaveChangesAsync();

        await SchemaMigrator.SeedDefaultMcpServersAsync(db);

        var rows = await db.McpServerDefinitions.AsNoTracking().ToListAsync();
        rows.Should().HaveCount(4);
        rows.First(r => r.Name == "web").Enabled.Should().BeFalse(
            "second seed must not destroy user-driven Enabled state");

        var marker = await db.SchemaVersions.FirstOrDefaultAsync(v => v.Key == SchemaMigrator.McpDefaultsMarker);
        marker.Should().NotBeNull();
    }

    [Fact]
    public async Task Seed_WipesPreExistingRows()
    {
        await using var db = NewDb();
        db.McpServerDefinitions.Add(new McpServerDefinitionEntity
        {
            Id = Guid.NewGuid(),
            Name = "old-stale-row",
            Transport = "Stdio",
            Command = "echo",
            ArgsJson = "[]",
            Enabled = true,
            IsBuiltIn = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await SchemaMigrator.SeedDefaultMcpServersAsync(db);

        var rows = await db.McpServerDefinitions.AsNoTracking().ToListAsync();
        rows.Should().HaveCount(4);
        rows.Should().NotContain(r => r.Name == "old-stale-row");
    }

    [Theory]
    [InlineData("web_fetch", "web.fetch")]
    [InlineData("shell", "shell.exec")]
    [InlineData("schedule", "scheduler.schedule")]
    [InlineData("read_file", "filesystem.read")]
    public void RemapEnabledToolsCsv_TranslatesKnownLegacyNames(string legacy, string expected)
    {
        var unmapped = new HashSet<string>(StringComparer.Ordinal);
        var result = SchemaMigrator.RemapEnabledToolsCsv(legacy, unmapped, out var changed);

        result.Should().Be(expected);
        changed.Should().BeTrue();
        unmapped.Should().BeEmpty();
    }

    [Fact]
    public void RemapEnabledToolsCsv_ExpandsMultiActionLegacyTools()
    {
        var unmapped = new HashSet<string>(StringComparer.Ordinal);
        var result = SchemaMigrator.RemapEnabledToolsCsv("file_system, browser", unmapped, out var changed);

        changed.Should().BeTrue();
        result.Should().Contain("filesystem.read");
        result.Should().Contain("filesystem.write");
        result.Should().Contain("browser.navigate");
        result.Should().Contain("browser.click");
        unmapped.Should().BeEmpty();
    }

    [Fact]
    public void RemapEnabledToolsCsv_PreservesAlreadyQualifiedNames()
    {
        var unmapped = new HashSet<string>(StringComparer.Ordinal);
        var result = SchemaMigrator.RemapEnabledToolsCsv("web.fetch, scheduler.schedule", unmapped, out var changed);

        changed.Should().BeFalse();
        result.Should().Be("web.fetch, scheduler.schedule");
    }

    [Fact]
    public void RemapEnabledToolsCsv_PreservesUnknownNamesAndReportsThem()
    {
        var unmapped = new HashSet<string>(StringComparer.Ordinal);
        var result = SchemaMigrator.RemapEnabledToolsCsv("totally_unknown", unmapped, out var changed);

        changed.Should().BeFalse();
        result.Should().Be("totally_unknown");
        unmapped.Should().Contain("totally_unknown");
    }

    [Fact]
    public async Task Seed_RemapsAgentProfileEnabledTools()
    {
        await using var db = NewDb();
        db.AgentProfiles.Add(new AgentProfileEntity
        {
            Name = "legacy-profile",
            EnabledTools = "web_fetch, shell, browser",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await SchemaMigrator.SeedDefaultMcpServersAsync(db);

        var profile = await db.AgentProfiles.AsNoTracking().FirstAsync(p => p.Name == "legacy-profile");
        profile.EnabledTools.Should().Contain("web.fetch");
        profile.EnabledTools.Should().Contain("shell.exec");
        profile.EnabledTools.Should().Contain("browser.navigate");
        profile.EnabledTools.Should().NotContain("web_fetch");
    }

    private static OpenClawDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<OpenClawDbContext>()
            .UseInMemoryDatabase($"schema-mig-{Guid.NewGuid()}")
            .Options;
        return new OpenClawDbContext(opts);
    }
}
