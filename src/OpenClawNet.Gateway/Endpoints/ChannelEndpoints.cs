using OpenClawNet.Models.Abstractions;

namespace OpenClawNet.Gateway.Endpoints;

/// <summary>
/// API endpoints for querying registered communication channels.
/// </summary>
public static class ChannelEndpoints
{
    /// <summary>
    /// Maps channel-related API endpoints onto the application's route table.
    /// </summary>
    public static IEndpointRouteBuilder MapChannelEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/channels", (IChannelRegistry registry) =>
        {
            var channels = registry.GetAllChannels()
                .Select(c => new { name = c.ChannelName, enabled = c.IsEnabled });

            return Results.Ok(channels);
        })
        .WithTags("Channels")
        .WithName("ListChannels")
        .WithDescription("Returns all registered communication channels and their enabled state.");

        return app;
    }
}
