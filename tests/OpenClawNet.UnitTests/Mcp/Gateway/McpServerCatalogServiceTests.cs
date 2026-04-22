using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;
using OpenClawNet.Gateway.Services.Mcp;
using OpenClawNet.Mcp.Abstractions;
using OpenClawNet.Mcp.Core;
using OpenClawNet.Storage;

namespace OpenClawNet.UnitTests.Mcp.Gateway;

public class McpServerCatalogServiceTests
{
    [Fact]
    public async Task Create_PersistsRowAndReturnsItem()
    {
        var (svc, _) = NewService();

        var (created, error) = await svc.CreateAsync(new McpServerWriteRequest
        {
            Name = "memory",
            Transport = "stdio",
            Command = "npx",
            Args = new() { "-y", "@modelcontextprotocol/server-memory" },
            Env = new() { ["GITHUB_TOKEN"] = "ghp_secret" },
        }, default);

        error.Should().BeNull();
        created.Should().NotBeNull();
        created!.Name.Should().Be("memory");
        created.HasEnv.Should().BeTrue();
        created.IsBuiltIn.Should().BeFalse();
    }

    [Fact]
    public async Task Create_RejectsDuplicateName()
    {
        var (svc, _) = NewService();
        await svc.CreateAsync(new McpServerWriteRequest { Name = "x", Transport = "stdio", Command = "echo" }, default);
        var (_, error) = await svc.CreateAsync(new McpServerWriteRequest { Name = "x", Transport = "stdio", Command = "echo" }, default);
        error.Should().Contain("already exists");
    }

    [Fact]
    public async Task Create_RejectsBuiltInName()
    {
        var (svc, _) = NewService(includeFakeBundled: true);
        var (_, error) = await svc.CreateAsync(new McpServerWriteRequest
        {
            Name = "fake-builtin", Transport = "stdio", Command = "echo",
        }, default);
        error.Should().Contain("reserved");
    }

    [Fact]
    public async Task Create_StdioWithoutCommand_Fails()
    {
        var (svc, _) = NewService();
        var (_, error) = await svc.CreateAsync(new McpServerWriteRequest { Name = "x", Transport = "stdio" }, default);
        error.Should().Contain("Command is required");
    }

    [Fact]
    public async Task Create_HttpWithoutUrl_Fails()
    {
        var (svc, _) = NewService();
        var (_, error) = await svc.CreateAsync(new McpServerWriteRequest { Name = "x", Transport = "http" }, default);
        error.Should().Contain("URL is required");
    }

    [Fact]
    public async Task Create_InProcessTransport_Forbidden()
    {
        var (svc, _) = NewService();
        var (_, error) = await svc.CreateAsync(new McpServerWriteRequest { Name = "x", Transport = "InProcess" }, default);
        error.Should().Contain("reserved for built-in");
    }

    [Fact]
    public async Task EncryptedEnv_RoundTripsThroughSecretStore()
    {
        var (svc, store) = NewService();
        await svc.CreateAsync(new McpServerWriteRequest
        {
            Name = "with-env", Transport = "stdio", Command = "echo",
            Env = new() { ["KEY"] = "value-1" },
        }, default);

        store.WrappedCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task List_IncludesBundledServersWithIsBuiltInTrue()
    {
        // PR-E: built-ins now live in the DB (seeded by SchemaMigrator). The fake bundled
        // registration alone is no longer surfaced — pre-seed a matching DB row.
        var (svc, _) = NewService(includeFakeBundled: true, seedFakeBuiltInRow: true);
        var items = await svc.ListAsync(default);

        items.Should().Contain(i => i.IsBuiltIn && i.Name == "fake-builtin");
    }

    [Fact]
    public async Task Update_BuiltIn_AllowsToggleEnabledOnly()
    {
        var (svc, _) = NewService(includeFakeBundled: true, seedFakeBuiltInRow: true);
        var items = await svc.ListAsync(default);
        var bundled = items.First(i => i.IsBuiltIn);

        // Allowed: enabled flip with no other field changes.
        var ok = await svc.UpdateAsync(bundled.Id, new McpServerWriteRequest
        {
            Name = bundled.Name, Transport = bundled.Transport, Enabled = false,
        }, default);
        ok.Forbidden.Should().BeFalse();
        ok.Item!.Enabled.Should().BeFalse();

        // Forbidden: tries to change the command.
        var forbidden = await svc.UpdateAsync(bundled.Id, new McpServerWriteRequest
        {
            Name = bundled.Name, Transport = bundled.Transport, Command = "/usr/bin/evil", Enabled = false,
        }, default);
        forbidden.Forbidden.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_BuiltIn_Forbidden()
    {
        var (svc, _) = NewService(includeFakeBundled: true, seedFakeBuiltInRow: true);
        var items = await svc.ListAsync(default);
        var bundled = items.First(i => i.IsBuiltIn);

        var result = await svc.DeleteAsync(bundled.Id, default);
        result.Forbidden.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_UserDefined_Removes()
    {
        var (svc, _) = NewService();
        var (created, _) = await svc.CreateAsync(new McpServerWriteRequest
        {
            Name = "deletable", Transport = "stdio", Command = "echo",
        }, default);

        var result = await svc.DeleteAsync(created!.Id, default);
        result.Deleted.Should().BeTrue();

        var items = await svc.ListAsync(default);
        items.Should().NotContain(i => i.Id == created.Id);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static (McpServerCatalogService svc, CountingSecretStore store) NewService(
        bool includeFakeBundled = false,
        bool seedFakeBuiltInRow = false)
    {
        var dbOptions = new DbContextOptionsBuilder<OpenClawDbContext>()
            .UseInMemoryDatabase($"mcp-svc-{Guid.NewGuid()}")
            .Options;
        var dbFactory = new InMemoryDbContextFactory(dbOptions);

        if (seedFakeBuiltInRow)
        {
            // PR-E: built-ins live in the DB after SchemaMigrator runs in production;
            // the unit-test fake never goes through that path so seed the row inline.
            using var db = dbFactory.CreateDbContext();
            db.McpServerDefinitions.Add(new OpenClawNet.Storage.Entities.McpServerDefinitionEntity
            {
                Id = FakeBundledRegistration.FixedId,
                Name = "fake-builtin",
                Transport = "InProcess",
                ArgsJson = "[]",
                Enabled = true,
                IsBuiltIn = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            db.SaveChanges();
        }

        var loggerFactory = NullLoggerFactory.Instance;
        var inProc = new InProcessMcpHost(loggerFactory, NullLogger<InProcessMcpHost>.Instance);
        var secrets = new CountingSecretStore();
        var stdio = new StdioMcpHost(secrets, NullLogger<StdioMcpHost>.Instance);

        var registrations = new List<IBundledMcpServerRegistration>();
        if (includeFakeBundled)
            registrations.Add(new FakeBundledRegistration());

        var bundled = new BundledMcpServerRegistry(registrations);

        // McpToolProvider needs a catalog; reuse the storage one (over the same in-mem DB).
        var catalog = new McpServerCatalog(dbFactory);
        var compositeCatalog = new CompositeMcpServerCatalog(catalog, bundled);
        var toolProvider = new McpToolProvider(compositeCatalog, inProc, stdio, NullLogger<McpToolProvider>.Instance);

        var svc = new McpServerCatalogService(
            dbFactory, secrets, toolProvider, inProc, stdio, bundled,
            NullLogger<McpServerCatalogService>.Instance);

        return (svc, secrets);
    }

    private sealed class InMemoryDbContextFactory : IDbContextFactory<OpenClawDbContext>
    {
        private readonly DbContextOptions<OpenClawDbContext> _options;
        public InMemoryDbContextFactory(DbContextOptions<OpenClawDbContext> options) => _options = options;
        public OpenClawDbContext CreateDbContext() => new(_options);
    }

    private sealed class CountingSecretStore : ISecretStore
    {
        public int WrappedCount;
        public string Protect(string plaintext) { WrappedCount++; return "enc:" + plaintext; }
        public string? Unprotect(string ciphertext) =>
            ciphertext.StartsWith("enc:") ? ciphertext[4..] : null;
    }

    private sealed class FakeBundledRegistration : IBundledMcpServerRegistration
    {
        public static readonly Guid FixedId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        public McpServerDefinition Definition { get; } = new()
        {
            Id = FixedId,
            Name = "fake-builtin",
            Transport = McpTransport.InProcess,
            Enabled = true,
            IsBuiltIn = true,
        };

        public IReadOnlyList<McpServerTool> CreateTools(IServiceProvider services) => Array.Empty<McpServerTool>();
    }
}
