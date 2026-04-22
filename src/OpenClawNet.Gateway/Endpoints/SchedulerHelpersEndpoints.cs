using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using OpenClawNet.Agent;
using OpenClawNet.Gateway.Services;
using OpenClawNet.Models.Abstractions;
using OpenClawNet.Storage;

namespace OpenClawNet.Gateway.Endpoints;

/// <summary>
/// Helper endpoints used by the Jobs UI — currently natural-language → cron translation.
/// Strategy: try a deterministic regex translator first (fast, free, offline).
/// Fall back to the default agent's chat client only when the regex can't match.
/// </summary>
public static class SchedulerHelpersEndpoints
{
    public static void MapSchedulerHelpersEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/scheduler").WithTags("SchedulerHelpers");

        group.MapPost("/translate-cron", TranslateCronAsync)
            .WithName("TranslateCron")
            .WithDescription("Convert a natural-language schedule description into a 5-field UNIX cron expression.");
    }

    internal static async Task<IResult> TranslateCronAsync(
        TranslateCronRequest? request,
        IAgentProfileStore profileStore,
        IServiceProvider services,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("TranslateCron");

        if (request is null || string.IsNullOrWhiteSpace(request.Text))
        {
            return Results.BadRequest(new { error = "Field 'text' is required and cannot be empty." });
        }

        var text = request.Text.Trim();

        if (CronTextTranslator.TryTranslate(text, out var regexCron, out var regexExplanation))
        {
            return Results.Ok(new TranslateCronResponse(regexCron, regexExplanation));
        }

        AgentProfile? profile;
        try
        {
            profile = await ResolveDefaultProfileAsync(profileStore, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve default agent profile for cron translation");
            return Results.Json(new { error = "No agent available." }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (profile is null)
        {
            return Results.Json(new { error = "No agent available." }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        IChatClient? chatClient;
        try
        {
            chatClient = ResolveChatClient(services, profile, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve chat client for provider '{Provider}'", profile.Provider);
            return Results.Json(new { error = "Chat client unavailable: " + ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (chatClient is null)
        {
            return Results.Json(new { error = "No chat client available for the default agent." }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var prompt = BuildPrompt(text);

        try
        {
            var response = await chatClient.GetResponseAsync(
                [new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, prompt)],
                cancellationToken: ct);

            var raw = response.Text ?? string.Empty;
            if (CronTextTranslator.TryParseLlmJson(raw, out var llmCron, out var llmExplanation, out var llmError))
            {
                return Results.Ok(new TranslateCronResponse(llmCron, llmExplanation));
            }

            return Results.BadRequest(new { error = llmError ?? "Could not parse a valid cron expression from the model response." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LLM call for cron translation failed");
            return Results.BadRequest(new { error = "Translation failed: " + ex.Message });
        }
    }

    /// <summary>
    /// Resolves the agent profile to use for cron text→expression translation.
    /// Order:
    /// 1. First enabled profile with <c>Kind=System</c> (preferred — these are
    ///    designated for internal platform tasks).
    /// 2. Profile flagged IsDefault=true (Standard fallback).
    /// 3. First enabled Standard profile by name (alphabetical).
    /// 4. null if no profiles exist.
    /// </summary>
    private static async Task<AgentProfile?> ResolveDefaultProfileAsync(
        IAgentProfileStore profileStore, CancellationToken ct)
    {
        var all = await profileStore.ListAsync(ct);
        if (all.Count == 0)
        {
            return null;
        }

        // Prefer a System-kind profile if available.
        var system = all.FirstOrDefault(p => p.Kind == ProfileKind.System && p.IsEnabled);
        if (system is not null)
        {
            return system;
        }

        var standard = all.Where(p => p.Kind == ProfileKind.Standard && p.IsEnabled).ToList();
        if (standard.Count == 0)
        {
            return null;
        }

        var flagged = standard.FirstOrDefault(p => p.IsDefault);
        return flagged ?? standard.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).First();
    }

    private static IChatClient? ResolveChatClient(
        IServiceProvider services, AgentProfile profile, CancellationToken ct)
    {
        var resolver = services.GetService<ProviderResolver>();
        ResolvedProviderConfig? resolved = null;
        if (resolver is not null && !string.IsNullOrEmpty(profile.Provider))
        {
            resolved = resolver.ResolveAsync(profile.Provider, ct).GetAwaiter().GetResult();
        }

        var providerType = resolved?.ProviderType ?? profile.Provider ?? "ollama";

        var providers = services.GetServices<IAgentProvider>();
        var provider = providers
            .Where(p => p.GetType().Name != "RuntimeAgentProvider")
            .FirstOrDefault(p => p.ProviderName.Equals(providerType, StringComparison.OrdinalIgnoreCase))
            ?? providers.FirstOrDefault();

        if (provider is null)
        {
            return null;
        }

        var enriched = new AgentProfile
        {
            Name = profile.Name,
            Provider = providerType,
            Endpoint = resolved?.Endpoint ?? profile.Endpoint,
            ApiKey = resolved?.ApiKey ?? profile.ApiKey,
            DeploymentName = resolved?.DeploymentName ?? profile.DeploymentName,
            AuthMode = resolved?.AuthMode ?? profile.AuthMode,
            Instructions = profile.Instructions
        };

        return provider.CreateChatClient(enriched);
    }

    private static string BuildPrompt(string text)
    {
        return
            "You convert natural-language schedule descriptions into a single 5-field UNIX cron expression.\n" +
            "Output STRICT JSON only, no markdown: {\"cron\":\"<expr>\",\"explanation\":\"<one short sentence>\"}.\n" +
            "If you cannot, output {\"error\":\"<reason>\"}.\n" +
            "Assume UTC unless the user says otherwise. Schedule: \"" + text + "\"";
    }
}

public sealed record TranslateCronRequest(string? Text);

public sealed record TranslateCronResponse(string Cron, string Explanation);

/// <summary>
/// Deterministic natural-language → cron translator. Covers the most common patterns
/// without needing an LLM round-trip. Returns false when no pattern matches.
/// </summary>
public static class CronTextTranslator
{
    private static readonly string[] DayNames =
    [
        "sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday"
    ];

    public static bool TryTranslate(string text, out string cron, out string explanation)
    {
        cron = string.Empty;
        explanation = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var t = text.Trim().ToLowerInvariant();

        var mEveryMin = Regex.Match(t, @"^every\s+(\d+)\s*minutes?$");
        if (mEveryMin.Success)
        {
            var n = int.Parse(mEveryMin.Groups[1].Value, CultureInfo.InvariantCulture);
            if (n is < 1 or > 59) return false;
            cron = "*/" + n + " * * * *";
            explanation = "Every " + n + " minutes.";
            return true;
        }

        if (Regex.IsMatch(t, @"^every\s+minute$"))
        {
            cron = "* * * * *";
            explanation = "Every minute.";
            return true;
        }

        var mEveryHr = Regex.Match(t, @"^every\s+(\d+)\s*hours?$");
        if (mEveryHr.Success)
        {
            var n = int.Parse(mEveryHr.Groups[1].Value, CultureInfo.InvariantCulture);
            if (n is < 1 or > 23) return false;
            cron = "0 */" + n + " * * *";
            explanation = "Every " + n + " hours, on the hour.";
            return true;
        }

        if (Regex.IsMatch(t, @"^(every\s+hour|hourly)$"))
        {
            cron = "0 * * * *";
            explanation = "Every hour, on the hour.";
            return true;
        }

        if (Regex.IsMatch(t, @"^(daily|every\s+day)$"))
        {
            cron = "0 0 * * *";
            explanation = "Every day at 00:00 UTC.";
            return true;
        }

        var mDailyAt = Regex.Match(t, @"^(?:daily|every\s+day)\s+at\s+(.+)$");
        if (mDailyAt.Success && TryParseTime(mDailyAt.Groups[1].Value, out var dh, out var dm))
        {
            cron = dm + " " + dh + " * * *";
            explanation = $"Every day at {dh:D2}:{dm:D2} UTC.";
            return true;
        }

        var mWeekday = Regex.Match(t, @"^(?:every\s+)?weekdays?\s+at\s+(.+)$");
        if (mWeekday.Success && TryParseTime(mWeekday.Groups[1].Value, out var wh, out var wm))
        {
            cron = wm + " " + wh + " * * 1-5";
            explanation = $"Every weekday at {wh:D2}:{wm:D2} UTC.";
            return true;
        }

        var mWeekend = Regex.Match(t, @"^(?:every\s+)?weekends?\s+at\s+(.+)$");
        if (mWeekend.Success && TryParseTime(mWeekend.Groups[1].Value, out var weh, out var wem))
        {
            cron = wem + " " + weh + " * * 0,6";
            explanation = $"Every Saturday and Sunday at {weh:D2}:{wem:D2} UTC.";
            return true;
        }

        var mDay = Regex.Match(t, @"^every\s+(sunday|monday|tuesday|wednesday|thursday|friday|saturday)(?:s)?(?:\s+at\s+(.+))?$");
        if (mDay.Success)
        {
            var dayIdx = Array.IndexOf(DayNames, mDay.Groups[1].Value);
            int hh = 0, mn = 0;
            if (mDay.Groups[2].Success && !TryParseTime(mDay.Groups[2].Value, out hh, out mn))
            {
                return false;
            }
            cron = mn + " " + hh + " * * " + dayIdx;
            var dayDisplay = char.ToUpperInvariant(mDay.Groups[1].Value[0]) + mDay.Groups[1].Value[1..];
            explanation = $"Every {dayDisplay} at {hh:D2}:{mn:D2} UTC.";
            return true;
        }

        var mAt = Regex.Match(t, @"^at\s+(.+)$");
        if (mAt.Success && TryParseTime(mAt.Groups[1].Value, out var ah, out var am))
        {
            cron = am + " " + ah + " * * *";
            explanation = $"Every day at {ah:D2}:{am:D2} UTC.";
            return true;
        }

        return false;
    }

    public static bool IsValidCron(string? cron)
    {
        if (string.IsNullOrWhiteSpace(cron)) return false;
        var parts = cron.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length is 5 or 6 && parts.All(p => !string.IsNullOrWhiteSpace(p));
    }

    public static bool TryParseLlmJson(string raw, out string cron, out string explanation, out string? error)
    {
        cron = string.Empty;
        explanation = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "Empty model response.";
            return false;
        }

        var first = raw.IndexOf('{');
        var last = raw.LastIndexOf('}');
        if (first < 0 || last <= first)
        {
            error = "Model response did not contain a JSON object.";
            return false;
        }

        var json = raw[first..(last + 1)];

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var errEl))
            {
                error = errEl.GetString() ?? "Model declined to translate.";
                return false;
            }

            if (!root.TryGetProperty("cron", out var cronEl) || cronEl.ValueKind != JsonValueKind.String)
            {
                error = "Model response missing 'cron' field.";
                return false;
            }

            var c = cronEl.GetString() ?? string.Empty;
            if (!IsValidCron(c))
            {
                error = "Model returned invalid cron expression: '" + c + "'.";
                return false;
            }

            cron = c.Trim();
            explanation = root.TryGetProperty("explanation", out var exEl)
                ? exEl.GetString() ?? string.Empty
                : string.Empty;
            return true;
        }
        catch (JsonException ex)
        {
            error = "Could not parse model JSON: " + ex.Message;
            return false;
        }
    }

    private static bool TryParseTime(string raw, out int hour, out int minute)
    {
        hour = 0;
        minute = 0;
        var s = raw.Trim().Trim('.', ',').ToLowerInvariant();

        s = Regex.Replace(s, @"\s+utc\b", string.Empty).Trim();

        if (s == "noon") { hour = 12; return true; }
        if (s == "midnight") { hour = 0; return true; }

        var m = Regex.Match(s, @"^(\d{1,2})(?::(\d{2}))?\s*(am|pm)?$");
        if (!m.Success) return false;

        var h = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var mi = m.Groups[2].Success ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
        var meridiem = m.Groups[3].Success ? m.Groups[3].Value : null;

        if (meridiem is not null)
        {
            if (h is < 1 or > 12) return false;
            if (meridiem == "am" && h == 12) h = 0;
            else if (meridiem == "pm" && h != 12) h += 12;
        }

        if (h is < 0 or > 23 || mi is < 0 or > 59) return false;

        hour = h;
        minute = mi;
        return true;
    }
}
