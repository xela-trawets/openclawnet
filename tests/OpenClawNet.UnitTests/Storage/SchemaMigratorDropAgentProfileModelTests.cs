using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpenClawNet.Storage;

namespace OpenClawNet.UnitTests.Storage;

/// <summary>
/// PR-F: covers <see cref="SchemaMigrator"/>'s removal of the legacy
/// <c>AgentProfiles.Model</c> column. Uses a real in-memory SQLite connection
/// because the migration step issues raw <c>ALTER TABLE … DROP COLUMN</c> SQL,
/// which the EF InMemory provider cannot execute.
/// </summary>
public class SchemaMigratorDropAgentProfileModelTests
{
    [Fact]
    public async Task Migrate_DropsLegacyModelColumn_WhenPresent()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        try
        {
            await using var db = new OpenClawDbContext(
                new DbContextOptionsBuilder<OpenClawDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();

            // Arrange — re-introduce the legacy Model column on the freshly created
            // schema to mimic an upgraded-from-pre-PR-F deployment.
            using (var seed = connection.CreateCommand())
            {
                seed.CommandText =
                    """
                    ALTER TABLE AgentProfiles ADD COLUMN Model TEXT;
                    INSERT INTO AgentProfiles (Name, Model, IsDefault, RequireToolApproval, IsEnabled, CreatedAt, UpdatedAt)
                    VALUES ('legacy', 'gpt-4o', 0, 0, 1, '2025-01-01T00:00:00', '2025-01-01T00:00:00');
                    """;
                await seed.ExecuteNonQueryAsync();
            }

            // Act
            await SchemaMigrator.MigrateAsync(db);

            // Assert — the column is gone and the row survived.
            var hasModel = await ColumnExistsAsync(connection, "AgentProfiles", "Model");
            hasModel.Should().BeFalse("PR-F drops AgentProfiles.Model");

            await using var freshDb = new OpenClawDbContext(
                new DbContextOptionsBuilder<OpenClawDbContext>().UseSqlite(connection).Options);
            var profile = await freshDb.AgentProfiles.FirstOrDefaultAsync(p => p.Name == "legacy");
            profile.Should().NotBeNull("dropping a column must not destroy the row");

            var marker = await freshDb.SchemaVersions
                .FirstOrDefaultAsync(v => v.Key == SchemaMigrator.AgentProfileDropModelMarker);
            marker.Should().NotBeNull("the migration must record a SchemaVersions marker");
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    [Fact]
    public async Task Migrate_IsNoOp_WhenColumnAlreadyAbsent()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        try
        {
            await using var db = new OpenClawDbContext(
                new DbContextOptionsBuilder<OpenClawDbContext>().UseSqlite(connection).Options);
            // Fresh DB built from the EF model — no Model column.
            await db.Database.EnsureCreatedAsync();

            // Act — running twice must not throw and must leave a single marker row.
            await SchemaMigrator.MigrateAsync(db);
            await SchemaMigrator.MigrateAsync(db);

            // Assert
            var hasModel = await ColumnExistsAsync(connection, "AgentProfiles", "Model");
            hasModel.Should().BeFalse();

            await using var freshDb = new OpenClawDbContext(
                new DbContextOptionsBuilder<OpenClawDbContext>().UseSqlite(connection).Options);
            var markers = await freshDb.SchemaVersions
                .Where(v => v.Key == SchemaMigrator.AgentProfileDropModelMarker)
                .CountAsync();
            markers.Should().Be(1, "the marker is keyed; a second migration run must not duplicate it");
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static async Task<bool> ColumnExistsAsync(SqliteConnection conn, string table, string column)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table})";
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader["name"]?.ToString(), column, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
