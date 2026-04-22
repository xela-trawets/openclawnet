namespace OpenClawNet.Gateway.Services;

/// <summary>
/// Structured result of parsing a natural-language schedule expression.
/// </summary>
public sealed record ParsedSchedule
{
    public string? CronExpression { get; init; }
    public DateTime? StartAt { get; init; }
    public DateTime? EndAt { get; init; }
    public string? TimeZone { get; init; }
    public bool IsRecurring { get; init; }
    public string? Description { get; init; }
    public DateTime? NextRunAt { get; init; }
}
