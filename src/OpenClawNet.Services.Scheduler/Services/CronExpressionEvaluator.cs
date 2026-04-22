using Cronos;
using Microsoft.Extensions.Logging;

namespace OpenClawNet.Services.Scheduler.Services;

/// <summary>
/// Utility for evaluating cron expressions using the Cronos library.
/// Wraps parsing and next-occurrence calculation with error handling.
/// </summary>
public sealed class CronExpressionEvaluator
{
    private readonly ILogger<CronExpressionEvaluator> _logger;

    public CronExpressionEvaluator(ILogger<CronExpressionEvaluator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses a cron expression and returns whether it's valid.
    /// </summary>
    public bool TryParse(string cronExpression, out CronExpression? parsed)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            parsed = null;
            return false;
        }

        try
        {
            var fieldCount = cronExpression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            var format = fieldCount >= 6 ? CronFormat.IncludeSeconds : CronFormat.Standard;
            parsed = CronExpression.Parse(cronExpression, format);
            return true;
        }
        catch (CronFormatException ex)
        {
            _logger.LogWarning(ex, "Invalid cron expression: {Expression}", cronExpression);
            parsed = null;
            return false;
        }
    }

    /// <summary>
    /// Calculates the next occurrence from a cron expression.
    /// </summary>
    public DateTime? GetNextOccurrence(
        string cronExpression,
        DateTime fromUtc,
        DateTime? endAt = null,
        string? timeZone = null)
    {
        if (!TryParse(cronExpression, out var cron) || cron is null)
            return null;

        try
        {
            var tz = ResolveTimeZone(timeZone);
            var next = cron.GetNextOccurrence(fromUtc, tz);

            if (next.HasValue && endAt.HasValue && next.Value > endAt.Value)
                return null;

            return next;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating next occurrence for cron: {Expression}", cronExpression);
            return null;
        }
    }

    /// <summary>
    /// Checks if a cron expression is due to run based on the current time.
    /// </summary>
    public bool IsDue(
        string cronExpression,
        DateTime? lastRunAt,
        DateTime nowUtc,
        string? timeZone = null)
    {
        var next = GetNextOccurrence(
            cronExpression,
            lastRunAt ?? nowUtc.AddMinutes(-1),
            null,
            timeZone);

        return next.HasValue && next.Value <= nowUtc;
    }

    private TimeZoneInfo ResolveTimeZone(string? timeZone)
    {
        if (string.IsNullOrEmpty(timeZone))
            return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        }
        catch (TimeZoneNotFoundException ex)
        {
            _logger.LogWarning(ex, "Timezone '{TimeZone}' not found, falling back to UTC", timeZone);
            return TimeZoneInfo.Utc;
        }
    }
}
