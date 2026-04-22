using System.Text.Json;
using OpenClawNet.Models.Abstractions;

namespace OpenClawNet.Gateway.Services;

/// <summary>
/// Uses the configured LLM to parse natural-language schedule expressions
/// into structured <see cref="ParsedSchedule"/> data.
/// </summary>
public sealed class SmartScheduleParser
{
    private readonly IModelClient _modelClient;
    private readonly ILogger<SmartScheduleParser> _logger;

    public SmartScheduleParser(IModelClient modelClient, ILogger<SmartScheduleParser> logger)
    {
        _modelClient = modelClient;
        _logger = logger;
    }

    private const string SystemPrompt = """
        You are a schedule-parsing assistant. The user will provide a natural-language schedule description.
        Parse it and return ONLY a JSON object (no markdown, no explanation) with these fields:

        {
          "cronExpression": "<5-field or 6-field cron>",
          "startAt": "<ISO 8601 UTC datetime or null>",
          "endAt": "<ISO 8601 UTC datetime or null>",
          "timeZone": "<IANA timezone string, default 'UTC'>",
          "isRecurring": <true|false>,
          "description": "<human-readable summary>"
        }

        Rules:
        - Standard cron: 5 fields (minute hour day month weekday). Example: "0 9 * * 1" = every Monday 9AM.
        - For sub-minute intervals (e.g., "every 30 seconds"), use 6-field cron with seconds as the FIRST field:
          second minute hour day month weekday. Example: "*/30 * * * * *" = every 30 seconds.
        - If the user specifies a timezone (EST, PST, CET, etc.), map it to an IANA timezone
          (e.g., EST → "America/New_York", PST → "America/Los_Angeles").
        - If no timezone is specified, use "UTC".
        - startAt/endAt should be ISO 8601 in UTC. If the user says "starting 2026-04-15 08:00 AM",
          convert to UTC based on the identified timezone.
        - For one-time schedules (not recurring), set isRecurring to false and put the run time in startAt.
          Leave cronExpression null for one-time jobs.
        - Return ONLY the JSON object. No markdown fences, no extra text.
        """;

    public async Task<ParsedSchedule?> ParseAsync(string input, CancellationToken ct = default)
    {
        _logger.LogInformation("Parsing schedule input: {Input}", input);

        var request = new ChatRequest
        {
            Messages =
            [
                new ChatMessage { Role = ChatMessageRole.System, Content = SystemPrompt },
                new ChatMessage { Role = ChatMessageRole.User, Content = input }
            ],
            Temperature = 0.0
        };

        var response = await _modelClient.CompleteAsync(request, ct);
        var raw = response.Content.Trim();

        _logger.LogDebug("LLM schedule response: {Raw}", raw);

        // Strip markdown code fences if the model wrapped the JSON
        if (raw.StartsWith("```"))
        {
            var firstNewline = raw.IndexOf('\n');
            var lastFence = raw.LastIndexOf("```");
            if (firstNewline > 0 && lastFence > firstNewline)
                raw = raw[(firstNewline + 1)..lastFence].Trim();
        }

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var parsed = JsonSerializer.Deserialize<ParsedSchedule>(raw, options);
            return parsed;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM schedule response as JSON: {Raw}", raw);
            return null;
        }
    }
}
