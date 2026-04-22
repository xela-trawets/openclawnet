using System.Runtime.InteropServices;

namespace OpenClawNet.Storage;

/// <summary>
/// Filesystem layout for local OpenClawNet artifacts: model caches, generated
/// binaries (PNGs, WAVs, etc.), and any other tool output that must persist
/// across runs but is not appropriate for the SQLite store.
/// </summary>
/// <remarks>
/// Bound from the <c>Storage</c> configuration section. Tools should depend on
/// <see cref="StorageOptions"/> rather than hard-coding paths so the user can
/// relocate everything (including multi-GB ONNX model caches) by changing one
/// setting.
/// </remarks>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Root directory for all local OpenClawNet storage. Defaults to
    /// <c>C:\openclawnet\storage</c> on Windows and
    /// <c>~/openclawnet/storage</c> elsewhere.
    /// </summary>
    public string RootPath { get; set; } = DefaultRootPath();

    /// <summary>
    /// Subfolder name (under <see cref="RootPath"/>) for binary artifacts
    /// produced by tools (images, audio, videos, etc.). Default: <c>binary</c>.
    /// </summary>
    public string BinaryFolderName { get; set; } = "binary";

    /// <summary>
    /// Subfolder name (under <see cref="RootPath"/>) for downloaded model
    /// caches (Stable Diffusion ONNX, Whisper, embeddings, etc.). Default: <c>models</c>.
    /// </summary>
    public string ModelsFolderName { get; set; } = "models";

    /// <summary>Absolute path for binary artifact outputs.</summary>
    public string BinaryArtifactsPath => Path.Combine(RootPath, BinaryFolderName);

    /// <summary>Absolute path for downloaded model caches.</summary>
    public string ModelsPath => Path.Combine(RootPath, ModelsFolderName);

    /// <summary>
    /// Returns (and creates if missing) a per-tool subfolder under the binary
    /// artifacts path. Example: <c>BinaryFolderForTool("text-to-image")</c> →
    /// <c>{RootPath}/binary/text-to-image/</c>.
    /// </summary>
    public string BinaryFolderForTool(string toolName)
    {
        var folder = Path.Combine(BinaryArtifactsPath, toolName);
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>Ensures <see cref="RootPath"/>, binary, and models directories exist.</summary>
    public void EnsureDirectories()
    {
        Directory.CreateDirectory(RootPath);
        Directory.CreateDirectory(BinaryArtifactsPath);
        Directory.CreateDirectory(ModelsPath);
    }

    private static string DefaultRootPath() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? @"C:\openclawnet\storage"
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "openclawnet", "storage");
}
