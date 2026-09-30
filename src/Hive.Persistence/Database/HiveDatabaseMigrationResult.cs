namespace Hive.Persistence;

public enum HiveDatabaseMigrationStatus
{
    Applied,
    AlreadyCurrent
}

public sealed record HiveDatabaseMigrationOutcome(
    HiveDatabaseMigrationStatus Status,
    int PreviousSchemaVersion,
    int CurrentSchemaVersion,
    int AppliedMigrationCount)
{
    public bool Changed => Status == HiveDatabaseMigrationStatus.Applied;
}