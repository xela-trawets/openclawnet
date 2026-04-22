using OpenClawNet.Web.Components;
using OpenClawNet.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Named HttpClient for Gateway API calls (used by Settings page and other pages)
builder.Services.AddHttpClient("gateway", (sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var gatewayUrl = config["Services:gateway:https:0"]
        ?? config["Services:gateway:http:0"]
        ?? config["OpenClawNet:GatewayBaseUrl"]
        ?? "https://localhost:7100";
    client.BaseAddress = new Uri(gatewayUrl.TrimEnd('/') + "/");
});

// Named HttpClient for Scheduler service
builder.Services.AddHttpClient("scheduler", (sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var schedulerUrl = config["Services:scheduler:https:0"]
        ?? config["Services:scheduler:http:0"]
        ?? "https://localhost:7200";
    client.BaseAddress = new Uri(schedulerUrl.TrimEnd('/') + "/");
});

// Typed clients for Jobs API
builder.Services.AddScoped<JobsClient>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
