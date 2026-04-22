using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using OpenClawNet.Services.Channels;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Bot Framework setup
builder.Services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();
builder.Services.AddSingleton<IBotFrameworkHttpAdapter, ChannelAdapterErrorHandler>();
builder.Services.AddHttpClient<GatewayForwardingBot>(c => c.BaseAddress = new Uri("https+http://gateway"));
builder.Services.AddTransient<IBot>(sp => sp.GetRequiredService<GatewayForwardingBot>());

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Teams Bot Framework webhook
app.MapPost("/api/messages", async (HttpContext ctx, IBotFrameworkHttpAdapter adapter, IBot bot) =>
    await adapter.ProcessAsync(ctx.Request, ctx.Response, bot))
    .WithTags("Channels")
    .WithName("TeamsWebhook");

app.Run();
