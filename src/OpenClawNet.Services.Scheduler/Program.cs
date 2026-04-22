using OpenClawNet.Services.Scheduler;
using OpenClawNet.Services.Scheduler.Components;
using OpenClawNet.Services.Scheduler.Endpoints;
using OpenClawNet.Services.Scheduler.Services;
using OpenClawNet.Storage;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddSqliteConnection("openclawnet-db");

builder.Services.AddOpenClawStorage();
builder.Services.AddSingleton<SchedulerSettingsService>();
builder.Services.AddSingleton<SchedulerRunState>();
builder.Services.AddSingleton<CronExpressionEvaluator>();
builder.Services.AddHostedService<SchedulerPollingService>();

builder.Services.AddHttpClient("gateway", c => c.BaseAddress = new Uri("https+http://gateway"));
// Self-referential client for Blazor components to call local API
builder.Services.AddHttpClient("scheduler-self", c => c.BaseAddress = new Uri("http://scheduler"));

// Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseAntiforgery();
app.UseStaticFiles();

// REST endpoints
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));
app.MapGet("/api/scheduler/status", (SchedulerRunState runState) =>
    Results.Ok(new { service = "scheduler", polling = true, running = runState.RunningJobCount }));
app.MapGet("/api/scheduler/running", (SchedulerRunState runState) =>
    Results.Ok(new { count = runState.RunningJobCount }));
app.MapSchedulerSettingsEndpoints();
app.MapSchedulerJobsEndpoints();

// Blazor UI
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
