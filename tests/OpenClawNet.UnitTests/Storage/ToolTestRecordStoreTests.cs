using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OpenClawNet.Storage;

namespace OpenClawNet.UnitTests.Storage;

public class ToolTestRecordStoreTests
{
    private static IDbContextFactory<OpenClawDbContext> CreateFactory()
    {
        var options = new DbContextOptionsBuilder<OpenClawDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new TestDbContextFactory(options);
    }

    [Fact]
    public async Task SaveAsync_ThenGetAsync_RoundTrips()
    {
        var store = new ToolTestRecordStore(CreateFactory());

        await store.SaveAsync("file_read", succeeded: true, message: "OK", mode: "direct");

        var rec = await store.GetAsync("file_read");
        rec.Should().NotBeNull();
        rec!.LastTestSucceeded.Should().BeTrue();
        rec.LastTestError.Should().Be("OK");
        rec.LastTestMode.Should().Be("direct");
        rec.LastTestedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SaveAsync_OverwritesExistingRecord()
    {
        var store = new ToolTestRecordStore(CreateFactory());
        await store.SaveAsync("shell_exec", succeeded: false, message: "boom", mode: "direct");
        await store.SaveAsync("shell_exec", succeeded: true, message: "fixed", mode: "probe");

        var rec = await store.GetAsync("shell_exec");
        rec!.LastTestSucceeded.Should().BeTrue();
        rec.LastTestError.Should().Be("fixed");
        rec.LastTestMode.Should().Be("probe");
    }

    [Fact]
    public async Task SaveAsync_TruncatesLongMessages()
    {
        var store = new ToolTestRecordStore(CreateFactory());
        var huge = new string('x', 5000);

        await store.SaveAsync("noisy", succeeded: false, message: huge, mode: "direct");

        var rec = await store.GetAsync("noisy");
        rec!.LastTestError!.Length.Should().BeLessThanOrEqualTo(1000);
    }

    [Fact]
    public async Task ListAsync_ReturnsAllRecords()
    {
        var store = new ToolTestRecordStore(CreateFactory());
        await store.SaveAsync("a", true, "ok", "direct");
        await store.SaveAsync("b", false, "fail", "probe");

        var list = await store.ListAsync();
        list.Should().HaveCount(2);
    }

    private sealed class TestDbContextFactory : IDbContextFactory<OpenClawDbContext>
    {
        private readonly DbContextOptions<OpenClawDbContext> _options;
        public TestDbContextFactory(DbContextOptions<OpenClawDbContext> options) => _options = options;
        public OpenClawDbContext CreateDbContext() => new(_options);
    }
}
