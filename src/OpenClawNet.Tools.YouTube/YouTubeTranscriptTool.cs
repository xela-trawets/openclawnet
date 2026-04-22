using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenClawNet.Tools.Abstractions;
using YoutubeExplode;
using YoutubeExplode.Videos;

namespace OpenClawNet.Tools.YouTube;

/// <summary>
/// Fetches a YouTube video's title, channel, duration, and (when available)
/// the closed-caption transcript via YoutubeExplode. Pure HTTP — no API key.
/// </summary>
public sealed class YouTubeTranscriptTool : ITool
{
    private readonly YoutubeClient _client;
    private readonly ILogger<YouTubeTranscriptTool> _logger;

    public YouTubeTranscriptTool(ILogger<YouTubeTranscriptTool> logger)
    {
        _client = new YoutubeClient();
        _logger = logger;
    }

    public string Name => "youtube_transcript";

    public string Description =>
        "Fetch a YouTube video's metadata (title, channel, duration) and its English closed-caption transcript " +
        "if one is published. Use this to summarize, quote, or search a video without watching it.";

    public ToolMetadata Metadata => new()
    {
        Name = Name,
        Description = Description,
        ParameterSchema = JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "url": { "type": "string", "description": "YouTube video URL or 11-character video ID" },
                "language": { "type": "string", "description": "Preferred caption language code (default: en). Falls back to first available." }
            },
            "required": ["url"]
        }
        """),
        RequiresApproval = false,
        Category = "web",
        Tags = ["youtube", "video", "transcript", "captions", "summarize"]
    };

    public async Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var url = input.GetStringArgument("url");
            var lang = input.GetStringArgument("language") ?? "en";
            if (string.IsNullOrWhiteSpace(url))
                return ToolResult.Fail(Name, "'url' is required", sw.Elapsed);

            VideoId videoId;
            try { videoId = VideoId.Parse(url); }
            catch (Exception)
            {
                return ToolResult.Fail(Name, $"Invalid YouTube URL or video ID: {url}", sw.Elapsed);
            }

            _logger.LogInformation("Fetching YouTube video metadata: {VideoId}", videoId);
            var video = await _client.Videos.GetAsync(videoId, cancellationToken);
            var manifest = await _client.Videos.ClosedCaptions.GetManifestAsync(videoId, cancellationToken);

            var sb = new StringBuilder();
            sb.AppendLine($"# {video.Title}");
            sb.AppendLine($"Channel: {video.Author.ChannelTitle}");
            sb.AppendLine($"Duration: {video.Duration?.ToString() ?? "(live)"}");
            sb.AppendLine($"URL: https://www.youtube.com/watch?v={videoId}");
            sb.AppendLine();

            // Pick the best track: requested language → English → first available
            var track = manifest.TryGetByLanguage(lang)
                ?? manifest.TryGetByLanguage("en")
                ?? manifest.Tracks.FirstOrDefault();

            if (track is null)
            {
                sb.AppendLine("(No closed captions available for this video.)");
                sw.Stop();
                return ToolResult.Ok(Name, sb.ToString(), sw.Elapsed);
            }

            var captions = await _client.Videos.ClosedCaptions.GetAsync(track, cancellationToken);
            sb.AppendLine($"## Transcript ({track.Language.Name})");
            foreach (var caption in captions.Captions)
            {
                if (string.IsNullOrWhiteSpace(caption.Text)) continue;
                sb.AppendLine(caption.Text);
            }

            sw.Stop();
            return ToolResult.Ok(Name, sb.ToString(), sw.Elapsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "YouTube transcript tool error");
            return ToolResult.Fail(Name, ex.Message, sw.Elapsed);
        }
    }
}
