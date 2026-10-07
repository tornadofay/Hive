using Hive.Core;

namespace Hive.Persistence;

internal sealed record EmbeddedPersistenceDatabaseStatus(
    HiveDatabaseState DatabaseState,
    int? SchemaVersion);
