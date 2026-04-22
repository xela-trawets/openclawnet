using Cronos;
using FluentAssertions;
using OpenClawNet.Services.Scheduler;
using OpenClawNet.Storage.Entities;

namespace OpenClawNet.UnitTests.Scheduler;

public sealed class SchedulerPollingServiceTests
{
    [Fact]
    public void CalculateNextRun_ValidCron_ReturnsNextOccurrence()
    {
        var cron = "0 9 * * *"; // Daily at 9 AM
        var from = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc);

        var next = SchedulerPollingService.CalculateNextRun(cron, from, null, null);

        next.Should().NotBeNull();
        next!.Value.Hour.Should().Be(9);
        next.Value.Minute.Should().Be(0);
    }

    [Fact]
    public void CalculateNextRun_InvalidCron_ReturnsNull()
    {
        var cron = "not a valid cron";
        var from = DateTime.UtcNow;

        var next = SchedulerPollingService.CalculateNextRun(cron, from, null, null);

        next.Should().BeNull();
    }

    [Fact]
    public void CalculateNextRun_PastEndAt_ReturnsNull()
    {
        var cron = "0 15 * * *"; // Daily at 3 PM
        var from = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var endAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc); // Ends at noon

        var next = SchedulerPollingService.CalculateNextRun(cron, from, endAt, null);

        // Next occurrence would be 3 PM, but schedule ends at noon
        next.Should().BeNull();
    }

    [Fact]
    public void CalculateNextRun_WithinEndAt_ReturnsValidTime()
    {
        var cron = "0 9 * * *"; // Daily at 9 AM
        var from = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var endAt = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        var next = SchedulerPollingService.CalculateNextRun(cron, from, endAt, null);

        next.Should().NotBeNull();
        next!.Value.Hour.Should().Be(9);
    }

    [Fact]
    public void CalculateNextRun_WithSeconds_ParsesCorrectly()
    {
        var cron = "30 0 9 * * *"; // Daily at 9:00:30 AM (6-field cron with seconds)
        var from = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc);

        var next = SchedulerPollingService.CalculateNextRun(cron, from, null, null);

        next.Should().NotBeNull();
        next!.Value.Hour.Should().Be(9);
        next.Value.Minute.Should().Be(0);
        next.Value.Second.Should().Be(30);
    }

    [Fact]
    public void CalculateNextRun_InvalidTimeZone_FallsBackToUtc()
    {
        var cron = "0 9 * * *";
        var from = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var invalidTz = "Invalid/Timezone";

        var next = SchedulerPollingService.CalculateNextRun(cron, from, null, invalidTz);

        // Should still calculate next run using UTC fallback
        next.Should().NotBeNull();
    }

    [Theory]
    [InlineData("0 9 * * *")]       // Standard 5-field
    [InlineData("0 0 9 * * *")]     // 6-field with seconds
    [InlineData("*/5 * * * *")]     // Every 5 minutes
    [InlineData("0 0 * * MON")]     // Every Monday at midnight
    public void CalculateNextRun_VariousCronFormats_ParsesSuccessfully(string cron)
    {
        var from = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var next = SchedulerPollingService.CalculateNextRun(cron, from, null, null);
        next.Should().NotBeNull();
    }

    [Fact]
    public void CalculateNextRun_RecurringJob_CalculatesMultipleOccurrences()
    {
        var cron = "*/10 * * * *"; // Every 10 minutes
        var from = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        var next1 = SchedulerPollingService.CalculateNextRun(cron, from, null, null);
        next1.Should().NotBeNull();
        next1!.Value.Minute.Should().Be(10);

        var next2 = SchedulerPollingService.CalculateNextRun(cron, next1.Value, null, null);
        next2.Should().NotBeNull();
        next2!.Value.Minute.Should().Be(20);

        var next3 = SchedulerPollingService.CalculateNextRun(cron, next2.Value, null, null);
        next3.Should().NotBeNull();
        next3!.Value.Minute.Should().Be(30);
    }
}
