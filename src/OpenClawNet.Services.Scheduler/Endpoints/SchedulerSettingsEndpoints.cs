using OpenClawNet.Services.Scheduler.Models;
using OpenClawNet.Services.Scheduler.Services;

namespace OpenClawNet.Services.Scheduler.Endpoints;

public static class SchedulerSettingsEndpoints
{
    public static void MapSchedulerSettingsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/scheduler/settings").WithTags("Scheduler");

        group.MapGet("/", (SchedulerSettingsService svc) =>
            Results.Ok(svc.GetSettings()))
            .WithName("GetSchedulerSettings");

        group.MapPut("/", (SchedulerSettings settings, SchedulerSettingsService svc) =>
        {
            svc.Update(settings);
            return Results.Ok(svc.GetSettings());
        })
        .WithName("UpdateSchedulerSettings");
    }
}
