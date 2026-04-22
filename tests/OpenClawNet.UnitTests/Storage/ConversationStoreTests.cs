using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OpenClawNet.Storage;

namespace OpenClawNet.UnitTests.Storage;

public class ConversationStoreTests : IDisposable
{
    private readonly IDbContextFactory<OpenClawDbContext> _factory;
    
    public ConversationStoreTests()
    {
        var options = new DbContextOptionsBuilder<OpenClawDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _factory = new TestDbContextFactory(options);
    }
    
    [Fact]
    public async Task CreateSession_ReturnsNewSession()
    {
        var store = new ConversationStore(_factory);
        
        var session = await store.CreateSessionAsync("Test Chat");
        
        session.Should().NotBeNull();
        session.Title.Should().Be("Test Chat");
        session.Id.Should().NotBeEmpty();
    }
    
    [Fact]
    public async Task GetSession_ReturnsNullWhenNotFound()
    {
        var store = new ConversationStore(_factory);
        
        var session = await store.GetSessionAsync(Guid.NewGuid());
        
        session.Should().BeNull();
    }
    
    [Fact]
    public async Task AddMessage_StoresMessage()
    {
        var store = new ConversationStore(_factory);
        var session = await store.CreateSessionAsync("Test");
        
        await store.AddMessageAsync(session.Id, "user", "Hello!");
        
        var messages = await store.GetMessagesAsync(session.Id);
        messages.Should().HaveCount(1);
        messages[0].Role.Should().Be("user");
        messages[0].Content.Should().Be("Hello!");
    }
    
    [Fact]
    public async Task AddMessage_IncrementsOrderIndex()
    {
        var store = new ConversationStore(_factory);
        var session = await store.CreateSessionAsync("Test");
        
        await store.AddMessageAsync(session.Id, "user", "First");
        await store.AddMessageAsync(session.Id, "assistant", "Second");
        
        var messages = await store.GetMessagesAsync(session.Id);
        messages.Should().HaveCount(2);
        messages[0].OrderIndex.Should().Be(0);
        messages[1].OrderIndex.Should().Be(1);
    }
    
    [Fact]
    public async Task ListSessions_ReturnsOrderedByUpdatedAt()
    {
        var store = new ConversationStore(_factory);
        
        var s1 = await store.CreateSessionAsync("First");
        await Task.Delay(10); // small delay for different timestamp
        var s2 = await store.CreateSessionAsync("Second");
        
        var sessions = await store.ListSessionsAsync();
        
        sessions.Should().HaveCountGreaterThanOrEqualTo(2);
        sessions[0].Id.Should().Be(s2.Id); // Most recently updated first
    }
    
    [Fact]
    public async Task DeleteSession_RemovesSession()
    {
        var store = new ConversationStore(_factory);
        var session = await store.CreateSessionAsync("Delete Me");
        
        await store.DeleteSessionAsync(session.Id);
        
        var result = await store.GetSessionAsync(session.Id);
        result.Should().BeNull();
    }
    
    [Fact]
    public async Task UpdateSessionTitle_ChangesTitle()
    {
        var store = new ConversationStore(_factory);
        var session = await store.CreateSessionAsync("Old Title");
        
        var updated = await store.UpdateSessionTitleAsync(session.Id, "New Title");
        
        updated.Title.Should().Be("New Title");
    }
    
    [Fact]
    public async Task AddMessage_AutoCreatesSession_WhenSessionNotFound()
    {
        var store = new ConversationStore(_factory);
        var unknownSessionId = Guid.NewGuid();

        // Should not throw even though session was never created
        await store.AddMessageAsync(unknownSessionId, "user", "Hello without a session!");

        var messages = await store.GetMessagesAsync(unknownSessionId);
        messages.Should().HaveCount(1);
        messages[0].Content.Should().Be("Hello without a session!");
    }

    [Fact]
    public async Task AddMessage_AutoCreatedSession_HasDefaultTitle()
    {
        var store = new ConversationStore(_factory);
        var unknownSessionId = Guid.NewGuid();

        await store.AddMessageAsync(unknownSessionId, "user", "Hi");

        var session = await store.GetSessionAsync(unknownSessionId);
        session.Should().NotBeNull();
        session!.Title.Should().NotBeNullOrEmpty();
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
