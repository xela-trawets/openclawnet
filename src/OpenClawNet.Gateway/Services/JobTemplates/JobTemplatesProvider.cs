using System.Reflection;
using System.Text.Json;
using OpenClawNet.Gateway.Endpoints;

namespace OpenClawNet.Gateway.Services.JobTemplates;

/// <summary>
/// Loads built-in <see cref="JobTemplate"/>s from embedded JSON resources at startup.
/// Resources must be added to the .csproj as <c>EmbeddedResource</c> under
/// <c>Resources/JobTemplates/*.json</c>. Each file is one template.
/// </summary>
/// <remarks>
/// This is a singleton with eager load — templates are immutable once the app starts,
/// so there is no benefit to lazy/per-request loading and a strong benefit to failing
/// fast at boot if a shipped resource is malformed.
/// </remarks>
public sealed class JobTemplatesProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly IReadOnlyDictionary<string, JobTemplate> _templates;

    public JobTemplatesProvider() : this(typeof(JobTemplatesProvider).Assembly) { }

    /// <summary>Test seam: load templates from a specific assembly (e.g. a fixture assembly).</summary>
    public JobTemplatesProvider(Assembly assembly)
    {
        const string prefix = "OpenClawNet.Gateway.Resources.JobTemplates.";
        var dict = new Dictionary<string, JobTemplate>(StringComparer.OrdinalIgnoreCase);

        foreach (var resource in assembly.GetManifestResourceNames())
        {
            if (!resource.StartsWith(prefix, StringComparison.Ordinal)) continue;
            if (!resource.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;

            using var stream = assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"Could not open template resource '{resource}'.");

            var template = JsonSerializer.Deserialize<JobTemplate>(stream, JsonOptions)
                ?? throw new InvalidOperationException($"Template resource '{resource}' deserialised to null.");

            if (string.IsNullOrWhiteSpace(template.Id))
                throw new InvalidOperationException($"Template resource '{resource}' is missing an Id.");

            if (dict.ContainsKey(template.Id))
                throw new InvalidOperationException($"Duplicate template id '{template.Id}' (resource '{resource}').");

            dict[template.Id] = template;
        }

        _templates = dict;
    }

    public IReadOnlyCollection<JobTemplate> GetAll() => _templates.Values.ToList();

    public JobTemplate? Get(string id) => _templates.TryGetValue(id, out var t) ? t : null;
}
