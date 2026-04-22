using System.Diagnostics;
using System.Text.Json;
using ElBruno.Text2Image;
using ElBruno.Text2Image.Models;
using Microsoft.Extensions.Logging;
using OpenClawNet.Storage;
using OpenClawNet.Tools.Abstractions;

namespace OpenClawNet.Tools.Text2Image;

/// <summary>
/// Generates an image from a text prompt using ElBruno.Text2Image (Stable Diffusion 1.5
/// via ONNX Runtime, CPU). The first invocation downloads the ~4 GB model from HuggingFace
/// to the user's profile cache; subsequent calls are fast.
/// </summary>
/// <remarks>
/// Output PNG is written under <c>{workspace}/.data/tool-outputs/text-to-image/{timestamp}.png</c>
/// and the absolute path is returned in the tool result so the agent can reference it.
/// </remarks>
public sealed class Text2ImageTool : ITool
{
    private readonly ILogger<Text2ImageTool> _logger;
    private readonly Text2ImageToolOptions _options;
    private readonly StorageOptions? _storage;

    public Text2ImageTool(ILogger<Text2ImageTool> logger, StorageOptions? storage = null, Text2ImageToolOptions? options = null)
    {
        _logger = logger;
        _storage = storage;
        _options = options ?? new Text2ImageToolOptions();
    }

    public string Name => "text_to_image";

    public string Description =>
        "Generate a PNG image from a text prompt using a local Stable Diffusion 1.5 model (ONNX). " +
        "The first call downloads the model (~4 GB) to the local HuggingFace cache. " +
        "Returns the absolute path of the generated PNG; the agent can reference or display it.";

    public ToolMetadata Metadata => new()
    {
        Name = Name,
        Description = Description,
        ParameterSchema = JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "prompt": { "type": "string", "description": "Text description of the image to generate" },
                "steps": { "type": "integer", "description": "Inference steps (default 15, more = higher quality, slower)" },
                "seed": { "type": "integer", "description": "Optional seed for reproducible output" },
                "width": { "type": "integer", "description": "Image width in pixels (default 512)" },
                "height": { "type": "integer", "description": "Image height in pixels (default 512)" }
            },
            "required": ["prompt"]
        }
        """),
        RequiresApproval = true,
        Category = "image",
        Tags = ["image", "generate", "stable-diffusion", "text-to-image", "ai"]
    };

    public async Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var prompt = input.GetStringArgument("prompt");
            if (string.IsNullOrWhiteSpace(prompt))
                return ToolResult.Fail(Name, "'prompt' is required", sw.Elapsed);

            var steps = ParseInt(input, "steps") ?? 15;
            var width = ParseInt(input, "width") ?? 512;
            var height = ParseInt(input, "height") ?? 512;
            var seed = ParseInt(input, "seed");

            var outDir = _options.OutputDirectory
                ?? _storage?.BinaryFolderForTool("text-to-image")
                ?? Path.Combine(Environment.CurrentDirectory, ".data", "tool-outputs", "text-to-image");
            Directory.CreateDirectory(outDir);
            var fileName = $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.png";
            var fullPath = Path.Combine(outDir, fileName);

            _logger.LogInformation("Generating image: prompt='{Prompt}' steps={Steps} {W}x{H}", prompt, steps, width, height);

            using var generator = new StableDiffusion15(new ImageGenerationOptions
            {
                NumInferenceSteps = steps,
                Width = width,
                Height = height,
                Seed = seed
            });

            await generator.EnsureModelAvailableAsync();
            var result = await generator.GenerateAsync(prompt, cancellationToken: cancellationToken);
            await result.SaveAsync(fullPath);
            sw.Stop();

            var msg = $"Saved to: {fullPath}\nInference: {result.InferenceTimeMs}ms\nSeed: {result.Seed}";
            return ToolResult.Ok(Name, msg, sw.Elapsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Text2Image tool error");
            return ToolResult.Fail(Name, ex.Message, sw.Elapsed);
        }
    }

    private static int? ParseInt(ToolInput input, string key)
    {
        // Accept either a JSON number or a string-encoded number.
        try
        {
            using var doc = JsonDocument.Parse(input.RawArguments);
            if (!doc.RootElement.TryGetProperty(key, out var v)) return null;
            return v.ValueKind switch
            {
                JsonValueKind.Number => v.GetInt32(),
                JsonValueKind.String when int.TryParse(v.GetString(), out var s) => s,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }
}

public sealed class Text2ImageToolOptions
{
    /// <summary>Override the output directory for generated PNGs.</summary>
    public string? OutputDirectory { get; set; }
}
