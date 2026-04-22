using System.Text.Json;
using OpenClawNet.Services.Scheduler.Models;

namespace OpenClawNet.Services.Scheduler.Services;

/// <summary>
/// Thread-safe singleton that holds the active <see cref="SchedulerSettings"/>.
/// Changes are persisted to scheduler-settings.json so they survive restarts.
/// </summary>
public sealed class SchedulerSettingsService
{
    private volatile SchedulerSettings _current;
    private readonly string _persistPath;
    private readonly ILogger<SchedulerSettingsService> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public SchedulerSettingsService(IConfiguration configuration, IHostEnvironment env, ILogger<SchedulerSettingsService> logger)
    {
        _logger = logger;
        _persistPath = Path.Combine(env.ContentRootPath, "scheduler-settings.json");
        _current = Load(configuration);
        _logger.LogInformation("SchedulerSettings loaded: MaxConcurrent={Max}, Timeout={Timeout}s, PollInterval={Poll}s",
            _current.MaxConcurrentJobs, _current.JobTimeoutSeconds, _current.PollIntervalSeconds);
    }

    public SchedulerSettings GetSettings() => _current;

    public void Update(SchedulerSettings settings)
    {
        if (settings.MaxConcurrentJobs < 1) settings.MaxConcurrentJobs = 1;
        if (settings.MaxConcurrentJobs > 20) settings.MaxConcurrentJobs = 20;
        if (settings.JobTimeoutSeconds < 10) settings.JobTimeoutSeconds = 10;
        if (settings.JobTimeoutSeconds > 3600) settings.JobTimeoutSeconds = 3600;
        if (settings.PollIntervalSeconds < 5) settings.PollIntervalSeconds = 5;
        if (settings.PollIntervalSeconds > 3600) settings.PollIntervalSeconds = 3600;

        Interlocked.Exchange(ref _current, settings);
        Persist(settings);
        _logger.LogInformation("SchedulerSettings updated: MaxConcurrent={Max}, Timeout={Timeout}s, PollInterval={Poll}s",
            settings.MaxConcurrentJobs, settings.JobTimeoutSeconds, settings.PollIntervalSeconds);
    }

    private SchedulerSettings Load(IConfiguration config)
    {
        if (File.Exists(_persistPath))
        {
            try
            {
                var json = File.ReadAllText(_persistPath);
                var loaded = JsonSerializer.Deserialize<SchedulerSettings>(json);
                if (loaded is not null)
                {
                    _logger.LogDebug("Loaded persisted scheduler settings from {Path}", _persistPath);
                    return loaded;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load persisted scheduler settings; using defaults");
            }
        }

        return new SchedulerSettings
        {
            MaxConcurrentJobs = config.GetValue("Scheduler:MaxConcurrentJobs", 3),
            JobTimeoutSeconds = config.GetValue("Scheduler:JobTimeoutSeconds", 300),
            PollIntervalSeconds = config.GetValue("Scheduler:PollIntervalSeconds", 30)
        };
    }

    private void Persist(SchedulerSettings settings)
    {
        try
        {
            File.WriteAllText(_persistPath, JsonSerializer.Serialize(settings, JsonOpts));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist scheduler settings to {Path}", _persistPath);
        }
    }
}
