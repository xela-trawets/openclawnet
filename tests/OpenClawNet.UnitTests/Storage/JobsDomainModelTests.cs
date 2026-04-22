using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpenClawNet.Storage;
using OpenClawNet.Storage.Entities;

namespace OpenClawNet.UnitTests.Storage;

public sealed class JobsDomainModelTests
{
    [Fact]
    public async Task ScheduledJob_NewColumns_CanBePersistedAndQueried()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<OpenClawDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new OpenClawDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var job = new ScheduledJob
        {
            Name = "Test Job",
            Prompt = "Test prompt with {{param1}} and {{param2}}",
            InputParametersJson = """{"param1": "value1", "param2": "value2"}""",
            LastOutputJson = """{"result": "success", "data": "output"}""",
            TriggerType = TriggerType.Webhook,
            WebhookEndpoint = "/webhooks/jobs/test-webhook",
            Status = JobStatus.Active
        };

        // Act
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        // Assert
        var retrieved = await db.Jobs.FirstOrDefaultAsync(j => j.Id == job.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("""{"param1": "value1", "param2": "value2"}""", retrieved.InputParametersJson);
        Assert.Equal("""{"result": "success", "data": "output"}""", retrieved.LastOutputJson);
        Assert.Equal(TriggerType.Webhook, retrieved.TriggerType);
        Assert.Equal("/webhooks/jobs/test-webhook", retrieved.WebhookEndpoint);
    }

    [Fact]
    public async Task JobRun_NewColumns_CanBePersistedAndQueried()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<OpenClawDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new OpenClawDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var job = new ScheduledJob
        {
            Name = "Test Job",
            Prompt = "Test prompt",
            Status = JobStatus.Active
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var run = new JobRun
        {
            JobId = job.Id,
            Status = "completed",
            Result = "Job completed successfully",
            InputSnapshotJson = """{"param1": "snapshot_value"}""",
            TokensUsed = 1250,
            ExecutedByAgentProfile = "gpt-5-mini-profile",
            CompletedAt = DateTime.UtcNow
        };

        // Act
        db.JobRuns.Add(run);
        await db.SaveChangesAsync();

        // Assert
        var retrieved = await db.JobRuns
            .Include(r => r.Job)
            .FirstOrDefaultAsync(r => r.Id == run.Id);
        
        Assert.NotNull(retrieved);
        Assert.Equal("""{"param1": "snapshot_value"}""", retrieved.InputSnapshotJson);
        Assert.Equal(1250, retrieved.TokensUsed);
        Assert.Equal("gpt-5-mini-profile", retrieved.ExecutedByAgentProfile);
    }

    [Fact]
    public async Task TriggerType_Enum_IsStoredAsString()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<OpenClawDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new OpenClawDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var cronJob = new ScheduledJob{ Name = "Cron", Prompt = "Test", TriggerType = TriggerType.Cron };
        var manualJob = new ScheduledJob { Name = "Manual", Prompt = "Test", TriggerType = TriggerType.Manual };
        var webhookJob = new ScheduledJob { Name = "Webhook", Prompt = "Test", TriggerType = TriggerType.Webhook };
        var oneshotJob = new ScheduledJob { Name = "OneShot", Prompt = "Test", TriggerType = TriggerType.OneShot };

        db.Jobs.AddRange(cronJob, manualJob, webhookJob, oneshotJob);
        await db.SaveChangesAsync();

        // Act - verify enum conversion round-trips correctly
        var retrieved = await db.Jobs.OrderBy(j => j.Name).ToListAsync();

        // Assert
        Assert.Equal(4, retrieved.Count);
        Assert.Equal(TriggerType.Cron, retrieved[0].TriggerType);
        Assert.Equal(TriggerType.Manual, retrieved[1].TriggerType);
        Assert.Equal(TriggerType.OneShot, retrieved[2].TriggerType);
        Assert.Equal(TriggerType.Webhook, retrieved[3].TriggerType);
    }

    [Fact]
    public async Task ScheduledJob_WithNullableColumns_CanBeCreatedWithDefaults()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<OpenClawDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new OpenClawDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var job = new ScheduledJob
        {
            Name = "Minimal Job",
            Prompt = "Just the basics",
            // All new columns should default to null or enum default
        };

        // Act
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        // Assert
        var retrieved = await db.Jobs.FirstAsync(j => j.Id == job.Id);
        Assert.Null(retrieved.InputParametersJson);
        Assert.Null(retrieved.LastOutputJson);
        Assert.Equal(TriggerType.Manual, retrieved.TriggerType); // default
        Assert.Null(retrieved.WebhookEndpoint);
    }
}
