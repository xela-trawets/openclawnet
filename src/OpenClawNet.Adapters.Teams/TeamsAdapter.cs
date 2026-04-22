using Microsoft.AspNetCore.Http;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Extensions.Logging;

namespace OpenClawNet.Adapters.Teams;

/// <summary>
/// IBotAdapter implementation for Microsoft Teams.
/// Wraps Bot Framework's CloudAdapter to process the /api/messages webhook.
/// </summary>
public sealed class TeamsAdapter : IBotAdapter
{
    private readonly IBotFrameworkHttpAdapter _cloudAdapter;
    private readonly IBot _bot;
    private readonly ILogger<TeamsAdapter> _logger;

    public string Platform => "teams";

    public TeamsAdapter(
        IBotFrameworkHttpAdapter cloudAdapter,
        IBot bot,
        ILogger<TeamsAdapter> logger)
    {
        _cloudAdapter = cloudAdapter;
        _bot = bot;
        _logger = logger;
    }

    public async Task HandleRequestAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Teams adapter processing request from {RemoteIp}", httpContext.Connection.RemoteIpAddress);
        await _cloudAdapter.ProcessAsync(httpContext.Request, httpContext.Response, _bot, cancellationToken);
    }
}
