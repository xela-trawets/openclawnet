using System.Diagnostics;
using System.Text.Json;
using ElBruno.QwenTTS.Pipeline;
using Microsoft.Extensions.Logging;
using OpenClawNet.Storage;
using OpenClawNet.Tools.Abstractions;

namespace OpenClawNet.Tools.TextToSpeech;

public sealed class TextToSpeechTool : ITool
{
    private static readonly string[] SupportedSpeakers =
        ["ryan", "serena", "vivian", "aiden", "eric", "dylan", "uncle_fu", "ono_anna", "sohee"];

    private static readonly string[] SupportedLanguages =
        ["english", "spanish", "chinese", "japanese", "korean"];

    private readonly StorageOptions _storage;
    private readonly ILogger<TextToSpeechTool> _logger;
    private TtsPipeline? _pipeline;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public TextToSpeechTool(StorageOptions storage, ILogger<TextToSpeechTool> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    public string Name => "text_to_speech";

    public string Description =>
        "Synthesize a 24 kHz mono WAV from text using the local Qwen3-TTS 0.6B model. " +
        "Returns the absolute path of the generated WAV. First call downloads ~5.5 GB.";

    public ToolMetadata Metadata => new()
    {
        Name = Name,
        Description = Description,
        ParameterSchema = JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "text": { "type": "string", "description": "Text to synthesize." },
                "speaker": { "type": "string", "enum": ["ryan","serena","vivian","aiden","eric","dylan","uncle_fu","ono_anna","sohee"], "description": "Voice (default: ryan)." },
                "language": { "type": "string", "enum": ["english","spanish","chinese","japanese","korean"], "description": "Language hint (default: english)." }
            },
            "required": ["text"]
        }
        """),
        RequiresApproval = true,
        Category = "audio",
        Tags = ["audio", "tts", "speech", "qwen"]
    };

    public async Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var text = input.GetStringArgument("text");
            if (string.IsNullOrWhiteSpace(text))
                return ToolResult.Fail(Name, "'text' is required", sw.Elapsed);

            var speaker = (input.GetStringArgument("speaker") ?? "ryan").ToLowerInvariant();
            if (!SupportedSpeakers.Contains(speaker))
                return ToolResult.Fail(Name, $"Unknown speaker '{speaker}'. Supported: {string.Join(", ", SupportedSpeakers)}", sw.Elapsed);

            var language = (input.GetStringArgument("language") ?? "english").ToLowerInvariant();
            if (!SupportedLanguages.Contains(language))
                return ToolResult.Fail(Name, $"Unknown language '{language}'. Supported: {string.Join(", ", SupportedLanguages)}", sw.Elapsed);

            var outDir = _storage.BinaryFolderForTool("text-to-speech");
            var fileName = $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{speaker}.wav";
            var fullPath = Path.Combine(outDir, fileName);

            var pipeline = await EnsurePipelineAsync(cancellationToken);
            _logger.LogInformation("Synthesizing speech: speaker={Speaker} lang={Lang} chars={Chars}", speaker, language, text.Length);
            await pipeline.SynthesizeAsync(text, speaker, fullPath, language);
            sw.Stop();

            return ToolResult.Ok(Name, $"Saved to: {fullPath}\nSpeaker: {speaker}\nLanguage: {language}", sw.Elapsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TextToSpeech tool error");
            return ToolResult.Fail(Name, ex.Message, sw.Elapsed);
        }
    }

    private async Task<TtsPipeline> EnsurePipelineAsync(CancellationToken ct)
    {
        if (_pipeline is not null) return _pipeline;
        await _initLock.WaitAsync(ct);
        try
        {
            if (_pipeline is not null) return _pipeline;
            var modelDir = Path.Combine(_storage.ModelsPath, "qwen-tts");
            Directory.CreateDirectory(modelDir);
            _pipeline = await TtsPipeline.CreateAsync(modelDir);
            return _pipeline;
        }
        finally { _initLock.Release(); }
    }
}
