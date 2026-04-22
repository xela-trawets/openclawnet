using Microsoft.AspNetCore.Mvc;
using OpenClawNet.Storage;

namespace OpenClawNet.Gateway.Endpoints;

/// <summary>
/// Encrypted secrets CRUD. Plaintext values are accepted on PUT and never
/// returned on GET — the listing endpoint exposes only name/description/timestamp.
/// </summary>
public static class SecretsEndpoints
{
    public static void MapSecretsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/secrets").WithTags("Secrets");

        group.MapGet("/", async (ISecretsStore store, CancellationToken ct) =>
            Results.Ok(await store.ListAsync(ct)))
            .WithName("ListSecrets")
            .WithDescription("Returns secret names and descriptions. Plaintext values are never returned.");

        group.MapPut("/{name}", async (string name, [FromBody] SecretWriteRequest req, ISecretsStore store, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(name)) return Results.BadRequest(new { error = "name is required" });
            if (req?.Value is null) return Results.BadRequest(new { error = "value is required" });
            await store.SetAsync(name, req.Value, req.Description, ct);
            return Results.NoContent();
        })
        .WithName("SetSecret")
        .WithDescription("Insert or update a secret. The plaintext is encrypted before persistence.");

        group.MapDelete("/{name}", async (string name, ISecretsStore store, CancellationToken ct) =>
            await store.DeleteAsync(name, ct) ? Results.NoContent() : Results.NotFound())
            .WithName("DeleteSecret");
    }

    public sealed record SecretWriteRequest(string Value, string? Description);
}
