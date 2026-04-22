using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenClawNet.Storage;
using Xunit;

namespace OpenClawNet.UnitTests.Storage;

public class SecretsStoreTests
{
    private static (SecretsStore Store, IDbContextFactory<OpenClawDbContext> Factory) CreateStore()
    {
        var dbName = $"secrets-{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddDataProtection();
        services.AddDbContextFactory<OpenClawDbContext>(o => o.UseInMemoryDatabase(dbName));
        var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IDbContextFactory<OpenClawDbContext>>();
        var dpp = sp.GetRequiredService<IDataProtectionProvider>();
        var store = new SecretsStore(factory, dpp);
        return (store, factory);
    }

    [Fact]
    public async Task Set_Then_Get_Roundtrips_Plaintext()
    {
        var (store, _) = CreateStore();
        await store.SetAsync("MY_KEY", "ghp_super_secret", "GitHub PAT");
        var value = await store.GetAsync("MY_KEY");
        Assert.Equal("ghp_super_secret", value);
    }

    [Fact]
    public async Task List_Does_Not_Return_Plaintext()
    {
        var (store, factory) = CreateStore();
        await store.SetAsync("API_KEY", "this-must-not-leak", "desc");
        var list = await store.ListAsync();
        var item = Assert.Single(list);
        Assert.Equal("API_KEY", item.Name);
        Assert.Equal("desc", item.Description);
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.Secrets.SingleAsync();
        Assert.NotEqual("this-must-not-leak", entity.EncryptedValue);
    }

    [Fact]
    public async Task Delete_Removes_Secret()
    {
        var (store, _) = CreateStore();
        await store.SetAsync("X", "v");
        await store.DeleteAsync("X");
        Assert.Null(await store.GetAsync("X"));
    }

    [Fact]
    public async Task Get_Missing_Returns_Null()
    {
        var (store, _) = CreateStore();
        Assert.Null(await store.GetAsync("DOES_NOT_EXIST"));
    }
}
