using Cronos;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenClawNet.Services.Scheduler.Services;

namespace OpenClawNet.UnitTests.Scheduler;

public sealed class CronExpressionEvaluatorTests
{
    private readonly CronExpressionEvaluator _evaluator;

    public CronExpressionEvaluatorTests()
    {
        _evaluator = new CronExpressionEvaluator(NullLogger<CronExpressionEvaluator>.Instance);
    }

    [Fact]
    public void TryParse_ValidStandardCron_ReturnsTrue()
    {
        // 5-field cron (minute, hour, day, month, day-of-week)
        var valid = _evaluator.TryParse("0 9 * * *", out var parsed);
        valid.Should().BeTrue();
        parsed.Should().NotBeNull();
    }

    [Fact]
    public void TryParse_ValidCronWithSeconds_ReturnsTrue()
    {
        // 6-field cron (seconds, minute, hour, day, month, day-of-week)
        var valid = _evaluator.TryParse("0 0 9 * * *", out var parsed);
        valid.Should().BeTrue();
        parsed.Should().NotBeNull();
    }

    [Fact]
    public void TryParse_InvalidCron_ReturnsFalse()
    {
        var valid = _evaluator.TryParse("not a cron", out var parsed);
        valid.Should().BeFalse();
        parsed.Should().BeNull();
    }

    [Fact]
    public void TryParse_EmptyString_ReturnsFalse()
    {
        var valid = _evaluator.TryParse("", out var parsed);
        valid.Should().BeFalse();
        parsed.Should().BeNull();
    }

    [Fact]
    public void TryParse_NullString_ReturnsFalse()
    {
        var valid = _evaluator.TryParse(null!, out var parsed);
        valid.Should().BeFalse();
        parsed.Should().BeNull();
    }

    [Fact]
    public void GetNextOccurrence_ValidCron_ReturnsNextTime()
    {
        // Every day at 9 AM
        var next = _evaluator.GetNextOccurrence(
            "0 9 * * *",
            new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc));

        next.Should().NotBeNull();
        next!.Value.Hour.Should().Be(9);
        next.Value.Minute.Should().Be(0);
    }

    [Fact]
    public void GetNextOccurrence_InvalidCron_ReturnsNull()
    {
        var next = _evaluator.GetNextOccurrence(
            "invalid cron",
            DateTime.UtcNow);

        next.Should().BeNull();
    }

    [Fact]
    public void GetNextOccurrence_RespectsEndAt_ReturnsNull()
    {
        var endAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var next = _evaluator.GetNextOccurrence(
            "0 15 * * *", // 3 PM daily
            new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc),
            endAt);

        // Next occurrence would be 3 PM, but endAt is noon, so should return null
        next.Should().BeNull();
    }

    [Fact]
    public void GetNextOccurrence_WithinEndAt_ReturnsValidTime()
    {
        var endAt = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var next = _evaluator.GetNextOccurrence(
            "0 9 * * *", // 9 AM daily
            new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc),
            endAt);

        next.Should().NotBeNull();
        next!.Value.Hour.Should().Be(9);
    }

    [Fact]
    public void IsDue_JobNeverRan_ReturnsTrue()
    {
        // Job scheduled for every minute, never ran
        var isDue = _evaluator.IsDue(
            "* * * * *",
            lastRunAt: null,
            DateTime.UtcNow);

        isDue.Should().BeTrue();
    }

    [Fact]
    public void IsDue_JobRanRecently_ReturnsFalse()
    {
        // Job runs daily at 9 AM, last ran 1 hour ago
        var lastRun = DateTime.UtcNow.AddHours(-1);
        var isDue = _evaluator.IsDue(
            "0 9 * * *",
            lastRun,
            DateTime.UtcNow);

        // If current time isn't 9 AM, it shouldn't be due
        isDue.Should().BeFalse();
    }

    [Fact]
    public void IsDue_InvalidCron_ReturnsFalse()
    {
        var isDue = _evaluator.IsDue(
            "not a cron",
            null,
            DateTime.UtcNow);

        isDue.Should().BeFalse();
    }

    [Theory]
    [InlineData("0 9 * * *")]      // Daily at 9 AM
    [InlineData("*/5 * * * *")]    // Every 5 minutes
    [InlineData("0 0 * * MON")]    // Every Monday at midnight
    [InlineData("0 12 1 * *")]     // 1st of every month at noon
    public void TryParse_VariousCronExpressions_ParsesSuccessfully(string cron)
    {
        var valid = _evaluator.TryParse(cron, out var parsed);
        valid.Should().BeTrue();
        parsed.Should().NotBeNull();
    }
}
