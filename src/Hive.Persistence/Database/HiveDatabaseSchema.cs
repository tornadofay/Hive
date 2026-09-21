namespace Hive.Persistence;

public static class HiveDatabaseSchema
{
    public const int CurrentSchemaVersion = 1;
    public const int MinimumSupportedSchemaVersion = 1;

    public const byte SchemaRowId = 1;

    public const string SchemaName = "dbo";
    public const string SchemaVersionTableName = "HiveSchemaVersion";
    public const string MigrationJournalTableName = "HiveMigrationJournal";
    public const string MigrationJournalColumnName = "ScriptName";
}