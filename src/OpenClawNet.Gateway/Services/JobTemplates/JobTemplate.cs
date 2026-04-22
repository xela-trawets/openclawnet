using OpenClawNet.Gateway.Endpoints;

namespace OpenClawNet.Gateway.Services.JobTemplates;

/// <summary>
/// A pre-canned job configuration shipped with OpenClawNet (or saved by a user)
/// that the UI can offer as a one-click starting point. Built-in templates are
/// loaded from embedded JSON resources under
/// <c>OpenClawNet.Gateway/Resources/JobTemplates/</c>.
/// </summary>
public sealed record JobTemplate
{
    /// <summary>Stable kebab-case id, used in URLs.</summary>
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    /// <summary>Free-form category for UI filtering, e.g. "files", "github", "media".</summary>
    public string? Category { get; init; }

    /// <summary>Link to the human walkthrough that this template was distilled from.</summary>
    public string? DocsUrl { get; init; }

    /// <summary>Human-readable prerequisites the user must satisfy before instantiating.</summary>
    public IReadOnlyList<string> Prerequisites { get; init; } = [];

    /// <summary>Names of secrets (looked up in ISecretsStore) the template assumes exist.</summary>
    public IReadOnlyList<string> RequiredSecrets { get; init; } = [];

    /// <summary>Tools the template's prompt expects to be enabled / approved.</summary>
    public IReadOnlyList<string> RequiredTools { get; init; } = [];

    /// <summary>The pre-filled job payload — ready to POST to <c>/api/jobs</c> as-is.</summary>
    public required CreateJobRequest DefaultJob { get; init; }
}
