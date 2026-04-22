using Microsoft.AspNetCore.Http;

namespace OpenClawNet.Adapters.Teams;

/// <summary>
/// Platform-agnostic adapter that routes incoming channel requests to the OpenClawNet agent runtime.
/// </summary>
public interface IBotAdapter
{
    /// <summary>Identifies the channel platform (e.g. "teams", "slack").</summary>
    string Platform { get; }

    /// <summary>Processes an inbound HTTP request from the channel platform.</summary>
    Task HandleRequestAsync(HttpContext httpContext, CancellationToken cancellationToken = default);
}
