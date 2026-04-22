using OpenClawNet.Models.Ollama;
using OpenClawNet.Services.Memory.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Register Ollama model client for summarization
builder.Services.AddOllama();

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));
app.MapMemoryEndpoints();
app.Run();
