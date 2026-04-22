using System.Text.Json;
using OpenClawNet.Tools.Abstractions;

namespace OpenClawNet.Gateway.Endpoints;

public static class ToolEndpoints
{
    public static void MapToolEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tools").WithTags("Tools");
        
        group.MapGet("/", (IToolRegistry registry) =>
        {
            var tools = registry.GetToolManifest();
            return Results.Ok(tools.Select(t => new ToolDto
            {
                Name = t.Name,
                Description = t.Description,
                Category = t.Category,
                RequiresApproval = t.RequiresApproval,
                Tags = t.Tags,
                ParameterSchema = JsonSerializer.Deserialize<JsonElement>(t.ParameterSchema.RootElement.GetRawText())
            }));
        })
        .WithName("ListTools");
    }
}

public sealed record ToolDto
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public bool RequiresApproval { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public JsonElement ParameterSchema { get; init; }
}
