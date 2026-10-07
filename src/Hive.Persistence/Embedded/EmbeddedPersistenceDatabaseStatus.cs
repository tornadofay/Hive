using Hive.Core;

namespace Hive.Persistence;

public sealed record EmbeddedPersistenceDatabaseStatus(
    HiveDatabaseState DatabaseState,
    int? SchemaVersion);
