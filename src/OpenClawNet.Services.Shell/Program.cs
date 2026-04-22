using OpenClawNet.Services.Shell;
using OpenClawNet.Services.Shell.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.Configure<ShellOptions>(builder.Configuration.GetSection("Services:Shell"));

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));
app.MapShellEndpoints();
app.Run();
