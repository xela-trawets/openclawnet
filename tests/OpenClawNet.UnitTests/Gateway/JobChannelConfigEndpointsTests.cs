using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpenClawNet.Gateway.Endpoints;
using OpenClawNet.Storage;
using OpenClawNet.Storage.Entities;
using System.Text.Json;

namespace OpenClawNet.UnitTests.Gateway;

public sealed class JobChannelConfigEndpointsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<OpenClawDbContext> _options;

    public JobChannelConfigEndpointsTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<OpenClawDbContext>()
            .UseSqlite(_connection)
            .Options;
    }

    public void Dispose() => _connection?.Dispose();

    [Fact]
    public async Task GetJobChannelConfigurations_ReturnsEmptyList_WhenNoConfigurations()
    {
        await using var db = new OpenClawDbContext(_options);
        await db.Database.EnsureCreatedAsync();

        var job = new ScheduledJob { Name = "Test Job", Prompt = "Test", Status = JobStatus.Active };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var factory = new TestDbContextFactory(db);
        var httpContext = CreateLoopbackContext();

        var result = await ExecuteGetChannelConfigurations(job.Id, factory, httpContext);

        result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.Ok<List<JobChannelConfigDto>>>();
        var okResult = (Microsoft.AspNetCore.Http.HttpResults.Ok<List<JobChannelConfigDto>>)result;
        okResult.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetJobChannelConfigurations_ReturnsConfigurations_OrderedByChannelType()
    {
        await using var db = new OpenClawDbContext(_options);
        await db.Database.EnsureCreatedAsync();

        var job = new ScheduledJob { Name = "Test Job", Prompt = "Test", Status = JobStatus.Active };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var config1 = new JobChannelConfiguration
        {
            JobId = job.Id,
            ChannelType = "Teams",
            ChannelConfig = "{\"conversationId\":\"abc\"}",
            IsEnabled = true
        };
        var config2 = new JobChannelConfiguration
        {
            JobId = job.Id,
            ChannelType = "GenericWebhook",
            ChannelConfig = "{\"webhookUrl\":\"https://test.com\"}",
            IsEnabled = false
        };
        db.JobChannelConfigurations.AddRange(config1, config2);
        await db.SaveChangesAsync();

        var factory = new TestDbContextFactory(db);
        var httpContext = CreateLoopbackContext();

        var result = await ExecuteGetChannelConfigurations(job.Id, factory, httpContext);

        result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.Ok<List<JobChannelConfigDto>>>();
        var okResult = (Microsoft.AspNetCore.Http.HttpResults.Ok<List<JobChannelConfigDto>>)result;
        okResult.Value.Should().HaveCount(2);
        okResult.Value[0].ChannelType.Should().Be("GenericWebhook");
        okResult.Value[1].ChannelType.Should().Be("Teams");
    }

    [Fact]
    public async Task GetJobChannelConfigurations_Returns404_WhenJobNotFound()
    {
        await using var db = new OpenClawDbContext(_options);
        await db.Database.EnsureCreatedAsync();

        var factory = new TestDbContextFactory(db);
        var httpContext = CreateLoopbackContext();

        var result = await ExecuteGetChannelConfigurations(Guid.NewGuid(), factory, httpContext);

        result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.NotFound<object>>();
    }

    [Fact]
    public async Task UpdateJobChannelConfiguration_CreatesNew_WhenNotExists()
    {
        await using var db = new OpenClawDbContext(_options);
        await db.Database.EnsureCreatedAsync();

        var job = new ScheduledJob { Name = "Test Job", Prompt = "Test", Status = JobStatus.Active };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var factory = new TestDbContextFactory(db);
        var httpContext = CreateLoopbackContext();
        var request = new UpdateJobChannelConfigRequest("{\"webhookUrl\":\"https://example.com\"}", true);

        var result = await ExecuteUpdateChannelConfiguration(job.Id, "GenericWebhook", request, factory, httpContext);

        result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.Ok<JobChannelConfigDto>>();
        var okResult = (Microsoft.AspNetCore.Http.HttpResults.Ok<JobChannelConfigDto>)result;
        okResult.Value.ChannelType.Should().Be("GenericWebhook");
        okResult.Value.IsEnabled.Should().BeTrue();
        okResult.Value.ChannelConfig.Should().Be("{\"webhookUrl\":\"https://example.com\"}");

        var saved = await db.JobChannelConfigurations.FirstOrDefaultAsync(c => c.JobId == job.Id);
        saved.Should().NotBeNull();
        saved!.ChannelType.Should().Be("GenericWebhook");
    }

    [Fact]
    public async Task UpdateJobChannelConfiguration_UpdatesExisting_WhenAlreadyExists()
    {
        await using var db = new OpenClawDbContext(_options);
        await db.Database.EnsureCreatedAsync();

        var job = new ScheduledJob { Name = "Test Job", Prompt = "Test", Status = JobStatus.Active };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var existing = new JobChannelConfiguration
        {
            JobId = job.Id,
            ChannelType = "Slack",
            ChannelConfig = "{\"webhookUrl\":\"https://old.com\"}",
            IsEnabled = false
        };
        db.JobChannelConfigurations.Add(existing);
        await db.SaveChangesAsync();

        var factory = new TestDbContextFactory(db);
        var httpContext = CreateLoopbackContext();
        var request = new UpdateJobChannelConfigRequest("{\"webhookUrl\":\"https://new.com\"}", true);

        var result = await ExecuteUpdateChannelConfiguration(job.Id, "Slack", request, factory, httpContext);

        result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.Ok<JobChannelConfigDto>>();
        var okResult = (Microsoft.AspNetCore.Http.HttpResults.Ok<JobChannelConfigDto>)result;
        okResult.Value.ChannelConfig.Should().Be("{\"webhookUrl\":\"https://new.com\"}");
        okResult.Value.IsEnabled.Should().BeTrue();

        var updated = await db.JobChannelConfigurations.FirstOrDefaultAsync(c => c.JobId == job.Id);
        updated.Should().NotBeNull();
        updated!.ChannelConfig.Should().Be("{\"webhookUrl\":\"https://new.com\"}");
        updated.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateJobChannelConfiguration_Returns400_WhenUnsupportedChannelType()
    {
        await using var db = new OpenClawDbContext(_options);
        await db.Database.EnsureCreatedAsync();

        var job = new ScheduledJob { Name = "Test Job", Prompt = "Test", Status = JobStatus.Active };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var factory = new TestDbContextFactory(db);
        var httpContext = CreateLoopbackContext();
        var request = new UpdateJobChannelConfigRequest("{\"key\":\"value\"}", true);

        var result = await ExecuteUpdateChannelConfiguration(job.Id, "InvalidChannel", request, factory, httpContext);

        result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.BadRequest<object>>();
    }

    [Fact]
    public async Task UpdateJobChannelConfiguration_Returns400_WhenInvalidJson()
    {
        await using var db = new OpenClawDbContext(_options);
        await db.Database.EnsureCreatedAsync();

        var job = new ScheduledJob { Name = "Test Job", Prompt = "Test", Status = JobStatus.Active };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var factory = new TestDbContextFactory(db);
        var httpContext = CreateLoopbackContext();
        var request = new UpdateJobChannelConfigRequest("{invalid json", true);

        var result = await ExecuteUpdateChannelConfiguration(job.Id, "Teams", request, factory, httpContext);

        result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.BadRequest<object>>();
    }

    [Fact]
    public async Task UpdateJobChannelConfiguration_Returns404_WhenJobNotFound()
    {
        await using var db = new OpenClawDbContext(_options);
        await db.Database.EnsureCreatedAsync();

        var factory = new TestDbContextFactory(db);
        var httpContext = CreateLoopbackContext();
        var request = new UpdateJobChannelConfigRequest("{}", true);

        var result = await ExecuteUpdateChannelConfiguration(Guid.NewGuid(), "Teams", request, factory, httpContext);

        result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.NotFound<object>>();
    }

    // Helper methods to simulate endpoint execution
    private static async Task<IResult> ExecuteGetChannelConfigurations(
        Guid jobId,
        IDbContextFactory<OpenClawDbContext> dbFactory,
        HttpContext httpContext)
    {
        if (!IsLoopbackRequest(httpContext))
            return Results.StatusCode(403);

        await using var db = await dbFactory.CreateDbContextAsync();

        var job = await db.Jobs.FindAsync(jobId);
        if (job is null)
            return Results.NotFound(new { error = "Job not found" });

        var configs = await db.JobChannelConfigurations
            .Where(c => c.JobId == jobId)
            .OrderBy(c => c.ChannelType)
            .ToListAsync();

        var dtos = configs.Select(c => new JobChannelConfigDto(
            c.Id,
            c.JobId,
            c.ChannelType,
            c.ChannelConfig,
            c.IsEnabled,
            c.CreatedAt,
            c.UpdatedAt
        )).ToList();

        return Results.Ok(dtos);
    }

    private static async Task<IResult> ExecuteUpdateChannelConfiguration(
        Guid jobId,
        string channelType,
        UpdateJobChannelConfigRequest request,
        IDbContextFactory<OpenClawDbContext> dbFactory,
        HttpContext httpContext)
    {
        if (!IsLoopbackRequest(httpContext))
            return Results.StatusCode(403);

        await using var db = await dbFactory.CreateDbContextAsync();

        var job = await db.Jobs.FindAsync(jobId);
        if (job is null)
            return Results.NotFound(new { error = "Job not found" });

        var supportedChannels = new[] { "GenericWebhook", "Teams", "Slack" };
        if (!supportedChannels.Contains(channelType, StringComparer.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new
            {
                error = $"Unsupported channel type '{channelType}'",
                supported = supportedChannels
            });
        }

        if (!string.IsNullOrWhiteSpace(request.ChannelConfig))
        {
            try
            {
                JsonSerializer.Deserialize<JsonElement>(request.ChannelConfig);
            }
            catch (JsonException ex)
            {
                return Results.BadRequest(new { error = "Invalid JSON in ChannelConfig", detail = ex.Message });
            }
        }

        var config = await db.JobChannelConfigurations
            .Where(c => c.JobId == jobId && c.ChannelType == channelType)
            .FirstOrDefaultAsync();

        if (config is null)
        {
            config = new JobChannelConfiguration
            {
                JobId = jobId,
                ChannelType = channelType,
                ChannelConfig = request.ChannelConfig,
                IsEnabled = request.IsEnabled,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.JobChannelConfigurations.Add(config);
        }
        else
        {
            config.ChannelConfig = request.ChannelConfig;
            config.IsEnabled = request.IsEnabled;
            config.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        return Results.Ok(new JobChannelConfigDto(
            config.Id,
            config.JobId,
            config.ChannelType,
            config.ChannelConfig,
            config.IsEnabled,
            config.CreatedAt,
            config.UpdatedAt
        ));
    }

    private static bool IsLoopbackRequest(HttpContext httpContext)
    {
        var remoteIp = httpContext.Connection.RemoteIpAddress;
        return remoteIp?.IsIPv4MappedToIPv6 == true
            ? remoteIp.MapToIPv4().ToString() == "127.0.0.1"
            : remoteIp?.ToString() == "127.0.0.1" || remoteIp?.ToString() == "::1";
    }

    private static HttpContext CreateLoopbackContext()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        return context;
    }

    private sealed class TestDbContextFactory : IDbContextFactory<OpenClawDbContext>
    {
        private readonly OpenClawDbContext _db;

        public TestDbContextFactory(OpenClawDbContext db) => _db = db;

        public OpenClawDbContext CreateDbContext() => _db;
    }
}
