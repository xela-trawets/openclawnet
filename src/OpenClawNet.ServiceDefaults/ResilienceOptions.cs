namespace OpenClawNet.ServiceDefaults;

/// <summary>
/// Configuration options for the standard HTTP resilience handler applied to every
/// HttpClient by <see cref="Microsoft.Extensions.Hosting.Extensions.AddServiceDefaults{TBuilder}"/>.
/// </summary>
/// <remarks>
/// Defaults are tuned for AI/LLM inference traffic — single-attempt requests can run
/// for up to two minutes and the overall budget allows for one retry plus headroom.
/// Bound from the <c>Resilience</c> configuration section.
/// </remarks>
public sealed class ResilienceOptions
{
    /// <summary>Overall budget for a logical request (across all retry attempts), in seconds.</summary>
    public int TotalRequestTimeoutSeconds { get; set; } = 300;

    /// <summary>Per-attempt HTTP timeout, in seconds.</summary>
    public int AttemptTimeoutSeconds { get; set; } = 120;

    /// <summary>Maximum retry attempts after the first failure.</summary>
    public int RetryMaxAttempts { get; set; } = 1;

    /// <summary>Circuit-breaker sampling window, in seconds. Must be ≥ 2× <see cref="AttemptTimeoutSeconds"/>.</summary>
    public int CircuitBreakerSamplingDurationSeconds { get; set; } = 300;
}
