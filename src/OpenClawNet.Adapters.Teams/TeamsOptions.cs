namespace OpenClawNet.Adapters.Teams;

/// <summary>
/// Configuration options for the Microsoft Teams bot adapter.
/// Bind from appsettings.json section "Teams".
/// </summary>
public sealed class TeamsOptions
{
    /// <summary>Set to true to activate the /api/messages webhook endpoint.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Azure Bot App ID (from Azure Bot resource).</summary>
    public string MicrosoftAppId { get; set; } = "";

    /// <summary>Azure Bot App Password / Client Secret.</summary>
    public string MicrosoftAppPassword { get; set; } = "";

    /// <summary>Tenant ID for single-tenant bots (leave empty for multi-tenant).</summary>
    public string MicrosoftAppTenantId { get; set; } = "";
}
