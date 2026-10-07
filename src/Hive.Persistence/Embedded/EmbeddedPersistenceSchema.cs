namespace Hive.Persistence;

internal static class EmbeddedPersistenceSchema
{
    public const int CurrentSchemaVersion = 15;
    public const int MinimumSupportedSchemaVersion = 1;
    public const int SchemaRowId = 1;
    public const string SchemaVersionTableName = "HiveSchemaVersion";
    public const string MigrationJournalTableName = "HiveMigrationJournal";
    public const string MigrationVersionColumnName = "MigrationVersion";
}
