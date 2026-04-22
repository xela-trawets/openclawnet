using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenClawNet.Storage;
using OpenClawNet.Tools.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace OpenClawNet.Tools.ImageEdit;

public sealed class ImageEditTool : ITool
{
    private readonly StorageOptions _storage;
    private readonly ILogger<ImageEditTool> _logger;

    public ImageEditTool(StorageOptions storage, ILogger<ImageEditTool> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    public string Name => "image_edit";

    public string Description =>
        "Edit a local image file with SixLabors.ImageSharp. Actions: resize (width+height), " +
        "convert (target format), crop (x,y,width,height). Returns the absolute path of the new file.";

    public ToolMetadata Metadata => new()
    {
        Name = Name,
        Description = Description,
        ParameterSchema = JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "action": { "type": "string", "enum": ["resize", "convert", "crop"], "description": "What to do with the image." },
                "input": { "type": "string", "description": "Absolute path to the input image file." },
                "format": { "type": "string", "enum": ["png", "jpeg", "webp"], "description": "Output format (default: same as input or png)." },
                "width": { "type": "integer", "description": "Target width (resize) or crop width." },
                "height": { "type": "integer", "description": "Target height (resize) or crop height." },
                "x": { "type": "integer", "description": "Crop X origin (crop only, default 0)." },
                "y": { "type": "integer", "description": "Crop Y origin (crop only, default 0)." }
            },
            "required": ["action", "input"]
        }
        """),
        RequiresApproval = false,
        Category = "image",
        Tags = ["image", "resize", "convert", "crop", "imagesharp"]
    };

    public async Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var action = (input.GetStringArgument("action") ?? "").ToLowerInvariant();
            var src = input.GetStringArgument("input");
            if (string.IsNullOrWhiteSpace(src) || !File.Exists(src))
                return ToolResult.Fail(Name, $"'input' file not found: {src}", sw.Elapsed);

            using var image = await Image.LoadAsync(src, cancellationToken);

            switch (action)
            {
                case "resize":
                {
                    var w = input.GetArgument<int?>("width") ?? 0;
                    var h = input.GetArgument<int?>("height") ?? 0;
                    if (w <= 0 && h <= 0)
                        return ToolResult.Fail(Name, "resize requires 'width' and/or 'height'", sw.Elapsed);
                    image.Mutate(c => c.Resize(w, h));
                    break;
                }
                case "convert":
                    break;
                case "crop":
                {
                    var w = input.GetArgument<int?>("width") ?? 0;
                    var h = input.GetArgument<int?>("height") ?? 0;
                    var x = input.GetArgument<int?>("x") ?? 0;
                    var y = input.GetArgument<int?>("y") ?? 0;
                    if (w <= 0 || h <= 0)
                        return ToolResult.Fail(Name, "crop requires positive 'width' and 'height'", sw.Elapsed);
                    image.Mutate(c => c.Crop(new Rectangle(x, y, w, h)));
                    break;
                }
                default:
                    return ToolResult.Fail(Name, $"Unknown action '{action}'", sw.Elapsed);
            }

            var rawFormat = input.GetStringArgument("format");
            var format = (rawFormat ?? Path.GetExtension(src).TrimStart('.')).ToLowerInvariant();
            if (format == "jpg") format = "jpeg";
            if (format != "png" && format != "jpeg" && format != "webp") format = "png";
            var outDir = _storage.BinaryFolderForTool("image-edit");
            var outName = $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{action}.{(format == "jpeg" ? "jpg" : format)}";
            var outPath = Path.Combine(outDir, outName);

            await using var fs = File.Create(outPath);
            switch (format)
            {
                case "jpeg": await image.SaveAsync(fs, new JpegEncoder(), cancellationToken); break;
                case "webp": await image.SaveAsync(fs, new WebpEncoder(), cancellationToken); break;
                default: await image.SaveAsync(fs, new PngEncoder(), cancellationToken); break;
            }
            sw.Stop();

            return ToolResult.Ok(Name,
                $"Saved to: {outPath}\nAction: {action}\nFormat: {format}\nFinal size: {image.Width}x{image.Height}",
                sw.Elapsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ImageEdit tool error");
            return ToolResult.Fail(Name, ex.Message, sw.Elapsed);
        }
    }
}
