using Hive.Core;

namespace Hive.Management;

public enum HivePersistenceMigrationDirection
{
    SqlServerToEmbedded,
    EmbeddedToSqlServer
}

public sealed record HivePersistenceMigrationRequest(
    HivePersistenceConfiguration SourceConfiguration,
    HivePersistenceConfiguration DestinationConfiguration);

public sealed record HivePersistenceMigrationResult(
    Guid MigrationId,
    HivePersistenceMigrationDirection Direction,
    HivePersistenceBackend SourceBackend,
    HivePersistenceBackend DestinationBackend,
    int SourceSchemaVersion,
    int DestinationSchemaVersion,
    long TotalRecordsMigrated,
    IReadOnlyDictionary<string, long> RecordCounts,
    bool SourceVerifiedUnchanged,
    bool DestinationVerified,
    bool DestinationActivated);

public interface IHivePersistenceMigrationQuiescence
{
    Task<Result<IAsyncDisposable>> AcquireAsync(
        CancellationToken cancellationToken = default);
}
