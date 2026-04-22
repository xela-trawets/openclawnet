namespace OpenClawNet.Storage.Entities;

/// <summary>
/// Tiny key/value table used by <see cref="SchemaMigrator"/> to record one-shot
/// migration markers. Each row represents a destructive or seed step that must
/// run exactly once per database.
/// </summary>
public class SchemaVersionEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime AppliedAt { get; set; }
}
