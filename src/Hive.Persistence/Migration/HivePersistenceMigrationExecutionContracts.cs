using Hive.Core;

namespace Hive.Persistence;

internal sealed record HivePersistenceMigrationExecutionResult(
    Guid MigrationId,
    HivePersistenceBackend SourceBackend,
    HivePersistenceBackend DestinationBackend,
    int SourceSchemaVersion,
    int DestinationSchemaVersion,
    long TotalRecordsMigrated,
    IReadOnlyDictionary<string, long> RecordCounts,
    bool SourceVerifiedUnchanged,
    bool DestinationVerified);
