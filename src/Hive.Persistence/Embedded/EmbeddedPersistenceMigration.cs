namespace Hive.Persistence;

internal sealed record EmbeddedPersistenceMigration(
    int Version,
    string Name,
    string Sql);
