using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Hive.Core;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

namespace Hive.Persistence;

public sealed class HivePersistenceDataMigrator
{
    private const string MigrationFailedCode = "hive.persistence.data-migration.failed";
    private const int CurrentSchemaVersion = HiveDatabaseSchema.CurrentSchemaVersion;

    private static readonly IReadOnlyList<HiveMigrationTableDefinition> Tables =
        new[]
        {
            new HiveMigrationTableDefinition(
                "HiveProviders",
                new[]
                {
                    C("ProviderId", HiveMigrationColumnKind.Guid),
                    C("ProviderKey", HiveMigrationColumnKind.String),
                    C("DisplayName", HiveMigrationColumnKind.String),
                    C("TransportKind", HiveMigrationColumnKind.String),
                    C("OwnerPrincipalId", HiveMigrationColumnKind.Guid),
                    C("ScopeKind", HiveMigrationColumnKind.Integer),
                    C("ScopeIdentity", HiveMigrationColumnKind.Guid, true),
                    C("ResourceVersion", HiveMigrationColumnKind.Integer),
                    C("CreatedByPrincipalId", HiveMigrationColumnKind.Guid),
                    C("CreatedAtUtc", HiveMigrationColumnKind.UtcDateTime),
                    C("CorrelationId", HiveMigrationColumnKind.Guid),
                    C("CausationId", HiveMigrationColumnKind.Guid, true),
                    C("LifecycleStatus", HiveMigrationColumnKind.Integer),
                    C("LifecycleChangedAtUtc", HiveMigrationColumnKind.UtcDateTime),
                    C("MetadataJson", HiveMigrationColumnKind.String)
                },
                "ProviderId"),

            new HiveMigrationTableDefinition(
                "HiveProviderAccounts",
                new[]
                {
                    C("ProviderAccountId", HiveMigrationColumnKind.Guid),
                    C("ProviderId", HiveMigrationColumnKind.Guid),
                    C("AccountKey", HiveMigrationColumnKind.String),
                    C("DisplayName", HiveMigrationColumnKind.String),
                    C("ExternalAccountId", HiveMigrationColumnKind.String, true),
                    C("OwnerPrincipalId", HiveMigrationColumnKind.Guid),
                    C("ScopeKind", HiveMigrationColumnKind.Integer),
                    C("ScopeIdentity", HiveMigrationColumnKind.Guid, true),
                    C("ResourceVersion", HiveMigrationColumnKind.Integer),
                    C("CreatedByPrincipalId", HiveMigrationColumnKind.Guid),
                    C("CreatedAtUtc", HiveMigrationColumnKind.UtcDateTime),
                    C("CorrelationId", HiveMigrationColumnKind.Guid),
                    C("CausationId", HiveMigrationColumnKind.Guid, true),
                    C("LifecycleStatus", HiveMigrationColumnKind.Integer),
                    C("LifecycleChangedAtUtc", HiveMigrationColumnKind.UtcDateTime),
                    C("MetadataJson", HiveMigrationColumnKind.String),
                    C("CredentialSecretId", HiveMigrationColumnKind.Guid, true)
                },
                "ProviderAccountId"),

            new HiveMigrationTableDefinition(
                "HiveExecutionTargets",
                new[]
                {
                    C("ExecutionTargetId", HiveMigrationColumnKind.Guid),
                    C("ProviderId", HiveMigrationColumnKind.Guid),
                    C("ProviderAccountId", HiveMigrationColumnKind.Guid),
                    C("TargetKey", HiveMigrationColumnKind.String),
                    C("DisplayName", HiveMigrationColumnKind.String),
                    C("EndpointUri", HiveMigrationColumnKind.String),
                    C("Model", HiveMigrationColumnKind.String, true),
                    C("Deployment", HiveMigrationColumnKind.String, true),
                    C("CapabilitiesJson", HiveMigrationColumnKind.String),
                    C("OwnerPrincipalId", HiveMigrationColumnKind.Guid),
                    C("ScopeKind", HiveMigrationColumnKind.Integer),
                    C("ScopeIdentity", HiveMigrationColumnKind.Guid, true),
                    C("ResourceVersion", HiveMigrationColumnKind.Integer),
                    C("CreatedByPrincipalId", HiveMigrationColumnKind.Guid),
                    C("CreatedAtUtc", HiveMigrationColumnKind.UtcDateTime),
                    C("CorrelationId", HiveMigrationColumnKind.Guid),
                    C("CausationId", HiveMigrationColumnKind.Guid, true),
                    C("LifecycleStatus", HiveMigrationColumnKind.Integer),
                    C("LifecycleChangedAtUtc", HiveMigrationColumnKind.UtcDateTime),
                    C("MetadataJson", HiveMigrationColumnKind.String)
                },
                "ExecutionTargetId"),

            new HiveMigrationTableDefinition(
                "HiveSecrets",
                new[]
                {
                    C("SecretId", HiveMigrationColumnKind.Guid),
                    C("SecretKey", HiveMigrationColumnKind.String),
                    C("DisplayName", HiveMigrationColumnKind.String),
                    C("OwnerPrincipalId", HiveMigrationColumnKind.Guid),
                    C("ScopeKind", HiveMigrationColumnKind.Integer),
                    C("ScopeIdentity", HiveMigrationColumnKind.Guid, true),
                    C("ResourceVersion", HiveMigrationColumnKind.Integer),
                    C("CreatedByPrincipalId", HiveMigrationColumnKind.Guid),
                    C("CreatedAtUtc", HiveMigrationColumnKind.UtcDateTime),
                    C("CorrelationId", HiveMigrationColumnKind.Guid),
                    C("CausationId", HiveMigrationColumnKind.Guid, true),
                    C("LifecycleStatus", HiveMigrationColumnKind.Integer),
                    C("LifecycleChangedAtUtc", HiveMigrationColumnKind.UtcDateTime),
                    C("MetadataJson", HiveMigrationColumnKind.String)
                },
                "SecretId"),

            new HiveMigrationTableDefinition(
                "HiveEventLog",
                new[]
                {
                    C("EventId", HiveMigrationColumnKind.Guid),
                    C("StreamKind", HiveMigrationColumnKind.Integer),
                    C("StreamIdentity", HiveMigrationColumnKind.Guid),
                    C("StreamVersion", HiveMigrationColumnKind.Integer),
                    C("OccurredAtUtc", HiveMigrationColumnKind.UtcDateTime),
                    C("EventType", HiveMigrationColumnKind.String),
                    C("PayloadSchemaVersion", HiveMigrationColumnKind.Integer),
                    C("CorrelationId", HiveMigrationColumnKind.Guid),
                    C("CausationId", HiveMigrationColumnKind.Guid, true),
                    C("PayloadJson", HiveMigrationColumnKind.String)
                },
                "StreamKind", "StreamIdentity", "StreamVersion"),

            new HiveMigrationTableDefinition(
                "HiveEventSnapshots",
                new[]
                {
                    C("StreamKind", HiveMigrationColumnKind.Integer),
                    C("StreamIdentity", HiveMigrationColumnKind.Guid),
                    C("SnapshotVersion", HiveMigrationColumnKind.Integer),
                    C("PayloadSchemaVersion", HiveMigrationColumnKind.Integer),
                    C("StateJson", HiveMigrationColumnKind.String),
                    C("UpdatedAtUtc", HiveMigrationColumnKind.UtcDateTime)
                },
                "StreamKind", "StreamIdentity"),

            new HiveMigrationTableDefinition(
                "HiveEventOutbox",
                new[]
                {
                    C("EventId", HiveMigrationColumnKind.Guid),
                    C("StreamKind", HiveMigrationColumnKind.Integer),
                    C("StreamIdentity", HiveMigrationColumnKind.Guid),
                    C("StreamVersion", HiveMigrationColumnKind.Integer),
                    C("OccurredAtUtc", HiveMigrationColumnKind.UtcDateTime),
                    C("EventType", HiveMigrationColumnKind.String),
                    C("PayloadSchemaVersion", HiveMigrationColumnKind.Integer),
                    C("CorrelationId", HiveMigrationColumnKind.Guid),
                    C("CausationId", HiveMigrationColumnKind.Guid, true),
                    C("PayloadJson", HiveMigrationColumnKind.String),
                    C("CreatedAtUtc", HiveMigrationColumnKind.UtcDateTime),
                    C("AttemptCount", HiveMigrationColumnKind.Integer),
                    C("LeaseId", HiveMigrationColumnKind.Guid, true),
                    C("LeaseExpiresAtUtc", HiveMigrationColumnKind.UtcDateTime, true)
                },
                "EventId"),

            new HiveMigrationTableDefinition(
                "HiveAgentDefinitions",
                new[]
                {
                    C("AgentDefinitionId", HiveMigrationColumnKind.Guid),
                    C("DefinitionKey", HiveMigrationColumnKind.String),
                    C("DisplayName", HiveMigrationColumnKind.String),
                    C("Generation", HiveMigrationColumnKind.Integer),
                    C("OwnerPrincipalId", HiveMigrationColumnKind.Guid),
                    C("ScopeKind", HiveMigrationColumnKind.Integer),
                    C("ScopeIdentity", HiveMigrationColumnKind.Guid, true),
                    C("ResourceVersion", HiveMigrationColumnKind.Integer),
                    C("CreatedByPrincipalId", HiveMigrationColumnKind.Guid),
                    C("CreatedAtUtc", HiveMigrationColumnKind.UtcDateTime),
                    C("CorrelationId", HiveMigrationColumnKind.Guid),
                    C("CausationId", HiveMigrationColumnKind.Guid, true),
                    C("LifecycleStatus", HiveMigrationColumnKind.Integer),
                    C("LifecycleChangedAtUtc", HiveMigrationColumnKind.UtcDateTime),
                    C("MetadataJson", HiveMigrationColumnKind.String),
                    C("ConfiguredExecutionTargetId", HiveMigrationColumnKind.Guid, true)
                },
                "AgentDefinitionId"),

            new HiveMigrationTableDefinition(
                "HiveWorkItems",
                new[]
                {
                    C("WorkItemId", HiveMigrationColumnKind.Guid),
                    C("OwnerPrincipalId", HiveMigrationColumnKind.Guid),
                    C("ScopeKind", HiveMigrationColumnKind.Integer),
                    C("ScopeIdentity", HiveMigrationColumnKind.Guid, true),
                    C("ResourceVersion", HiveMigrationColumnKind.Integer),
                    C("CreatedByPrincipalId", HiveMigrationColumnKind.Guid),
                    C("CreatedAtUtc", HiveMigrationColumnKind.UtcDateTime),
                    C("CorrelationId", HiveMigrationColumnKind.Guid),
                    C("CausationId", HiveMigrationColumnKind.Guid, true),
                    C("SourceKind", HiveMigrationColumnKind.Integer, true),
                    C("SourceIdentity", HiveMigrationColumnKind.Guid, true),
                    C("LifecycleStatus", HiveMigrationColumnKind.Integer),
                    C("LifecycleChangedAtUtc", HiveMigrationColumnKind.UtcDateTime),
                    C("Status", HiveMigrationColumnKind.Integer),
                    C("MetadataJson", HiveMigrationColumnKind.String),
                    C("AttachmentFileName", HiveMigrationColumnKind.String, true),
                    C("AttachmentMediaType", HiveMigrationColumnKind.String, true),
                    C("AttachmentContentLength", HiveMigrationColumnKind.Integer, true),
                    C("AttachmentSha256", HiveMigrationColumnKind.String, true)
                },
                "WorkItemId"),

            new HiveMigrationTableDefinition(
                "HiveWorkItemAttachments",
                new[]
                {
                    C("WorkItemId", HiveMigrationColumnKind.Guid),
                    C("FileName", HiveMigrationColumnKind.String),
                    C("MediaType", HiveMigrationColumnKind.String),
                    C("ContentLength", HiveMigrationColumnKind.Integer),
                    C("Sha256", HiveMigrationColumnKind.String),
                    C("Content", HiveMigrationColumnKind.Binary),
                    C("CreatedAtUtc", HiveMigrationColumnKind.UtcDateTime)
                },
                "WorkItemId"),

            new HiveMigrationTableDefinition(
                "HiveExecutionTargetFavorites",
                new[]
                {
                    C("DeploymentId", HiveMigrationColumnKind.Guid),
                    C("OwnerPrincipalId", HiveMigrationColumnKind.Guid),
                    C("ScopeKind", HiveMigrationColumnKind.Integer),
                    C("ScopeIdentity", HiveMigrationColumnKind.Guid),
                    C("ExecutionTargetId", HiveMigrationColumnKind.Guid),
                    C("DisplayOrder", HiveMigrationColumnKind.Integer)
                },
                "DeploymentId", "OwnerPrincipalId", "ScopeKind", "ScopeIdentity", "DisplayOrder", "ExecutionTargetId"),

            new HiveMigrationTableDefinition(
                "HiveAgentWorkState",
                new[]
                {
                    C("WorkStateKind", HiveMigrationColumnKind.Integer),
                    C("StateId", HiveMigrationColumnKind.Guid),
                    C("AgentId", HiveMigrationColumnKind.Guid),
                    C("RuntimeId", HiveMigrationColumnKind.Guid),
                    C("OwnerPrincipalId", HiveMigrationColumnKind.Guid),
                    C("ScopeKind", HiveMigrationColumnKind.Integer),
                    C("ScopeIdentity", HiveMigrationColumnKind.Guid),
                    C("ResourceVersion", HiveMigrationColumnKind.Integer),
                    C("CreatedAtUtc", HiveMigrationColumnKind.UtcDateTime),
                    C("CorrelationId", HiveMigrationColumnKind.Guid),
                    C("CausationId", HiveMigrationColumnKind.Guid, true),
                    C("SourceKind", HiveMigrationColumnKind.Integer, true),
                    C("SourceIdentity", HiveMigrationColumnKind.Guid, true),
                    C("LifecycleStatus", HiveMigrationColumnKind.Integer),
                    C("LifecycleChangedAtUtc", HiveMigrationColumnKind.UtcDateTime),
                    C("StateStatus", HiveMigrationColumnKind.Integer),
                    C("StateKey", HiveMigrationColumnKind.String, true),
                    C("ExpiresAtUtc", HiveMigrationColumnKind.UtcDateTime, true),
                    C("StateJson", HiveMigrationColumnKind.String),
                    C("UpdatedAtUtc", HiveMigrationColumnKind.UtcDateTime)
                },
                "WorkStateKind", "StateId")
        };

    private static HiveMigrationColumnDefinition C(
        string name,
        HiveMigrationColumnKind kind,
        bool nullable = false) =>
        new(name, kind, nullable);

    public async Task<Result<HivePersistenceMigrationExecutionResult>> MigrateAsync(
        HivePersistenceConfiguration sourceConfiguration,
        SecretMaterial? sourceSqlCredential,
        HivePersistenceConfiguration destinationConfiguration,
        SecretMaterial? destinationSqlCredential,
        Guid migrationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceConfiguration);
        ArgumentNullException.ThrowIfNull(destinationConfiguration);

        if (migrationId == Guid.Empty)
            throw new ArgumentException("Migration identity is required.", nameof(migrationId));

        if (sourceConfiguration.Backend == destinationConfiguration.Backend)
        {
            return Result<HivePersistenceMigrationExecutionResult>.Failure(
                Error.Validation(
                    "hive.persistence.data-migration.backend-direction-invalid",
                    "Persistence migration requires different source and destination backends."));
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return sourceConfiguration.Backend == HivePersistenceBackend.SqlServer
                ? await MigrateSqlToEmbeddedAsync(
                    sourceConfiguration,
                    sourceSqlCredential,
                    destinationConfiguration,
                    migrationId,
                    cancellationToken).ConfigureAwait(false)
                : await MigrateEmbeddedToSqlAsync(
                    sourceConfiguration,
                    destinationSqlCredential,
                    destinationConfiguration,
                    migrationId,
                    cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result<HivePersistenceMigrationExecutionResult>.Failure(
                HivePersistenceError.External(
                    MigrationFailedCode,
                    "The Hive persistence data migration failed.",
                    exception));
        }
    }

    private async Task<Result<HivePersistenceMigrationExecutionResult>> MigrateSqlToEmbeddedAsync(
        HivePersistenceConfiguration sourceConfiguration,
        SecretMaterial? sourceSqlCredential,
        HivePersistenceConfiguration destinationConfiguration,
        Guid migrationId,
        CancellationToken cancellationToken)
    {
        var sourceOptions = HiveDatabaseOptions.FromConfiguration(
            sourceConfiguration,
            sourceSqlCredential);

        var sourcePreflight = await InspectSqlDatabaseAsync(
            sourceOptions,
            cancellationToken).ConfigureAwait(false);

        if (sourcePreflight.IsFailure)
            return Result<HivePersistenceMigrationExecutionResult>.Failure(sourcePreflight.Error!);

        await using var destination = new EmbeddedPersistenceDatabase(
            destinationConfiguration);

        var destinationPreflight = await PrepareEmbeddedDestinationAsync(
            destination,
            cancellationToken).ConfigureAwait(false);

        if (destinationPreflight.IsFailure)
            return Result<HivePersistenceMigrationExecutionResult>.Failure(
                destinationPreflight.Error!);

        await using var sourceConnection = new SqlConnection(
            sourceOptions.ConnectionString);

        await sourceConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

        return await TransferAndVerifyAsync(
            migrationId,
            HivePersistenceBackend.SqlServer,
            HivePersistenceBackend.Embedded,
            sourceConnection,
            destination,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<HivePersistenceMigrationExecutionResult>> MigrateEmbeddedToSqlAsync(
        HivePersistenceConfiguration sourceConfiguration,
        SecretMaterial? sourceSqlCredential,
        HivePersistenceConfiguration destinationConfiguration,
        Guid migrationId,
        CancellationToken cancellationToken)
    {
        await using var source = new EmbeddedPersistenceDatabase(
            sourceConfiguration);

        var sourceStatus = await source.InspectAsync(
            cancellationToken).ConfigureAwait(false);

        if (sourceStatus.IsFailure)
            return Result<HivePersistenceMigrationExecutionResult>.Failure(
                sourceStatus.Error!);

        if (sourceStatus.Value!.DatabaseState != HiveDatabaseState.Current ||
            sourceStatus.Value.SchemaVersion != CurrentSchemaVersion)
        {
            return Result<HivePersistenceMigrationExecutionResult>.Failure(
                MigrationIncompatibleSourceError(
                    sourceStatus.Value.DatabaseState,
                    sourceStatus.Value.SchemaVersion));
        }

        var sourceShape = await ValidateEmbeddedDatabaseShapeAsync(
            source,
            cancellationToken).ConfigureAwait(false);

        if (sourceShape.IsFailure)
            return Result<HivePersistenceMigrationExecutionResult>.Failure(
                sourceShape.Error!);

            var destinationOptions = HiveDatabaseOptions.FromConfiguration(
                destinationConfiguration,
                sourceSqlCredential);

            var destinationPreflight = await PrepareSqlDestinationAsync(
                destinationOptions,
                cancellationToken).ConfigureAwait(false);

            if (destinationPreflight.IsFailure)
                return Result<HivePersistenceMigrationExecutionResult>.Failure(
                    destinationPreflight.Error!);

            await using var sourceConnection = await source.OpenConnectionAsync(
                cancellationToken).ConfigureAwait(false);

            await using var destinationConnection = new SqlConnection(
                destinationOptions.ConnectionString);

            await destinationConnection.OpenAsync(
                cancellationToken).ConfigureAwait(false);

        return await TransferAndVerifyAsync(
            migrationId,
            HivePersistenceBackend.Embedded,
            HivePersistenceBackend.SqlServer,
            sourceConnection,
            destinationConnection,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<HivePersistenceMigrationExecutionResult>> TransferAndVerifyAsync(
        Guid migrationId,
        HivePersistenceBackend sourceBackend,
        HivePersistenceBackend destinationBackend,
        DbConnection sourceConnection,
        object destinationDatabase,
        CancellationToken cancellationToken)
    {
        if (destinationBackend == HivePersistenceBackend.Embedded)
        {
            var destination = (EmbeddedPersistenceDatabase)destinationDatabase;
            await using var destinationConnection = await destination.OpenConnectionAsync(
                cancellationToken).ConfigureAwait(false);

            return await TransferAndVerifyCoreAsync(
                migrationId,
                sourceBackend,
                destinationBackend,
                sourceConnection,
                destinationConnection,
                cancellationToken).ConfigureAwait(false);
        }

        var sqlDestination = (SqlConnection)destinationDatabase;
        return await TransferAndVerifyCoreAsync(
            migrationId,
            sourceBackend,
            destinationBackend,
            sourceConnection,
            sqlDestination,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<HivePersistenceMigrationExecutionResult>> TransferAndVerifyCoreAsync(
        Guid migrationId,
        HivePersistenceBackend sourceBackend,
        HivePersistenceBackend destinationBackend,
        DbConnection sourceConnection,
        DbConnection destinationConnection,
        CancellationToken cancellationToken)
    {
        using var sourceFingerprint = new HiveMigrationFingerprintSet();
        var rowCounts = new Dictionary<string, long>(StringComparer.Ordinal);
        long total = 0;

        await using var transaction = await destinationConnection.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);

        foreach (var table in Tables)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (table.Name == "HiveSecrets")
            {
                await TransferSecretsAsync(
                    sourceConnection,
                    destinationConnection,
                    transaction,
                    table,
                    sourceFingerprint,
                    rowCounts,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await TransferTableAsync(
                    sourceConnection,
                    destinationConnection,
                    transaction,
                    table,
                    sourceFingerprint,
                    rowCounts,
                    cancellationToken).ConfigureAwait(false);
            }

            total += rowCounts[table.Name];
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var destinationFingerprint = await ReadFingerprintAsync(
            destinationConnection,
            destinationBackend,
            transaction,
            cancellationToken).ConfigureAwait(false);

        if (!sourceFingerprint.Matches(destinationFingerprint))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result<HivePersistenceMigrationExecutionResult>.Failure(
                Error.Conflict(
                    "hive.persistence.data-migration.verification-failed",
                    "The migration destination does not exactly match the source logical dataset."));
        }

        using var sourceAfter = await ReadFingerprintAsync(
            sourceConnection,
            sourceBackend,
            null,
            cancellationToken).ConfigureAwait(false);

        if (!sourceFingerprint.Matches(sourceAfter))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result<HivePersistenceMigrationExecutionResult>.Failure(
                Error.Concurrency(
                    "hive.persistence.data-migration.source-changed",
                    "The source data changed while the migration was running."));
        }

        cancellationToken.ThrowIfCancellationRequested();
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result<HivePersistenceMigrationExecutionResult>.Success(
            new HivePersistenceMigrationExecutionResult(
                migrationId,
                sourceBackend,
                destinationBackend,
                CurrentSchemaVersion,
                CurrentSchemaVersion,
                total,
                new ReadOnlyDictionary<string, long>(rowCounts),
                SourceVerifiedUnchanged: true,
                DestinationVerified: true));
    }

    private async Task TransferTableAsync(
        DbConnection sourceConnection,
        DbConnection destinationConnection,
        DbTransaction destinationTransaction,
        HiveMigrationTableDefinition table,
        HiveMigrationFingerprintSet fingerprints,
        Dictionary<string, long> counts,
        CancellationToken cancellationToken)
    {
        var select = BuildSelectSql(
            table,
            sourceConnection is SqliteConnection);

        await using var sourceCommand = sourceConnection.CreateCommand();
        sourceCommand.CommandText = select;

        await using var reader = await sourceCommand.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        await using var destinationCommand = destinationConnection.CreateCommand();
        destinationCommand.Transaction = destinationTransaction;
        destinationCommand.CommandText = BuildInsertSql(table, destinationConnection is SqliteConnection);
        AddParameters(destinationCommand, table);

        long count = 0;

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var values = ReadRow(reader, table);
            fingerprints.AddRow(table.Name, table.Columns, values);

            BindParameters(
                destinationCommand,
                table.Columns,
                values);

            _ = await destinationCommand.ExecuteNonQueryAsync(
                cancellationToken).ConfigureAwait(false);

            count++;
        }

        counts[table.Name] = count;
    }

    private async Task TransferSecretsAsync(
        DbConnection sourceConnection,
        DbConnection destinationConnection,
        DbTransaction destinationTransaction,
        HiveMigrationTableDefinition table,
        HiveMigrationFingerprintSet fingerprints,
        Dictionary<string, long> counts,
        CancellationToken cancellationToken)
    {
        const string encryptedColumn = "EncryptedValue";

        var selectColumns = string.Join(
            ", ",
            table.Columns
                .Select(static c => $"[{c.Name}]")
                .Append($"[{encryptedColumn}]"));

        var orderBy = string.Join(
            ", ",
            table.OrderByColumns.Select(static c => $"[{c}]"));

        var qualifiedTable = QuoteTable(
            table.Name,
            sourceConnection is SqliteConnection);

        await using var sourceCommand = sourceConnection.CreateCommand();
        sourceCommand.CommandText = $"SELECT {selectColumns} FROM {qualifiedTable} ORDER BY {orderBy};";

        await using var reader = await sourceCommand.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var destinationTable = $"{table.Name}";
        var destinationColumns = table.Columns
            .Select(static c => c.Name)
            .Append(encryptedColumn)
            .ToArray();

        await using var destinationCommand = destinationConnection.CreateCommand();
        destinationCommand.Transaction = destinationTransaction;
        destinationCommand.CommandText = BuildInsertSql(
            destinationTable,
            destinationColumns,
            destinationConnection is SqliteConnection);

        AddParameters(
            destinationCommand,
            table.Columns.Concat(
                new[]
                {
                    new HiveMigrationColumnDefinition(
                        encryptedColumn,
                        HiveMigrationColumnKind.Binary)
                }).ToArray());

        long count = 0;

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var values = ReadRow(reader, table);
            var encrypted = ReadBinary(
                reader,
                table.Columns.Count);

            using var material = DpapiSecretProtection.Unprotect(encrypted);

            fingerprints.AddRow(
                table.Name,
                table.Columns,
                values,
                material.Reveal());

            var destinationEncrypted = DpapiSecretProtection.Protect(material);

            var destinationValues = values
                .Append(destinationEncrypted)
                .ToArray();

            BindParameters(
                destinationCommand,
                table.Columns.Concat(
                    new[]
                    {
                        new HiveMigrationColumnDefinition(
                            encryptedColumn,
                            HiveMigrationColumnKind.Binary)
                    }).ToArray(),
                destinationValues);

            try
            {
                _ = await destinationCommand.ExecuteNonQueryAsync(
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(destinationEncrypted);
            }

            count++;
        }

        counts[table.Name] = count;
    }

    private async Task<HiveMigrationFingerprintSet> ReadFingerprintAsync(
        DbConnection connection,
        HivePersistenceBackend backend,
        DbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var set = new HiveMigrationFingerprintSet();

        foreach (var table in Tables)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (table.Name == "HiveSecrets")
            {
                await FingerprintSecretsAsync(
                    connection,
                    table,
                    set,
                    backend,
                    transaction,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await FingerprintTableAsync(
                    connection,
                    table,
                    set,
                    transaction,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        return set;
    }

    private async Task FingerprintTableAsync(
        DbConnection connection,
        HiveMigrationTableDefinition table,
        HiveMigrationFingerprintSet set,
        DbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = BuildSelectSql(
            table,
            connection is SqliteConnection);

        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            set.AddRow(
                table.Name,
                table.Columns,
                ReadRow(reader, table));
        }
    }

    private async Task FingerprintSecretsAsync(
        DbConnection connection,
        HiveMigrationTableDefinition table,
        HiveMigrationFingerprintSet set,
        HivePersistenceBackend backend,
        DbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        const string encryptedColumn = "EncryptedValue";
        var selectColumns = string.Join(
            ", ",
            table.Columns
                .Select(static c => $"[{c.Name}]")
                .Append($"[{encryptedColumn}]"));

        var qualifiedTable = QuoteTable(
            table.Name,
            connection is SqliteConnection);

        var orderBy = string.Join(
            ", ",
            table.OrderByColumns.Select(static c => $"[{c}]"));

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT {selectColumns} FROM {qualifiedTable} ORDER BY {orderBy};";

        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var values = ReadRow(reader, table);
            var encrypted = ReadBinary(
                reader,
                table.Columns.Count);

            using var material = DpapiSecretProtection.Unprotect(encrypted);
            set.AddRow(
                table.Name,
                table.Columns,
                values,
                material.Reveal());
        }
    }

    private static object?[] ReadRow(
        DbDataReader reader,
        HiveMigrationTableDefinition table)
    {
        var values = new object?[table.Columns.Count];

        for (var i = 0; i < table.Columns.Count; i++)
        {
            values[i] = ReadValue(
                reader,
                i,
                table.Columns[i].Kind);
        }

        return values;
    }

    private static object? ReadValue(
        DbDataReader reader,
        int ordinal,
        HiveMigrationColumnKind kind)
    {
        if (reader.IsDBNull(ordinal))
            return null;

        var value = reader.GetValue(ordinal);

        return kind switch
        {
            HiveMigrationColumnKind.Guid =>
                value is Guid guid
                    ? guid
                    : Guid.Parse(
                        Convert.ToString(
                            value,
                            CultureInfo.InvariantCulture)!),

            HiveMigrationColumnKind.String =>
                Convert.ToString(
                    value,
                    CultureInfo.InvariantCulture),

            HiveMigrationColumnKind.Integer =>
                Convert.ToInt64(
                    value,
                    CultureInfo.InvariantCulture),

            HiveMigrationColumnKind.UtcDateTime =>
                ReadUtcDateTime(value),

            HiveMigrationColumnKind.Binary =>
                ReadBinary(value),

            _ => throw new InvalidOperationException(
                $"Unsupported migration column kind: {kind}.")
        };
    }

    private static DateTimeOffset ReadUtcDateTime(object value)
    {
        if (value is DateTimeOffset offset)
            return offset.ToUniversalTime();

        if (value is DateTime dateTime)
        {
            return new DateTimeOffset(
                DateTime.SpecifyKind(
                    dateTime,
                    DateTimeKind.Utc));
        }

        if (value is string text)
        {
            return DateTimeOffset.Parse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind).ToUniversalTime();
        }

        throw new InvalidOperationException(
            "Persisted migration timestamp has an unsupported data representation.");
    }

    private static byte[] ReadBinary(object value)
    {
        return value switch
        {
            byte[] bytes => bytes.ToArray(),
            _ => throw new InvalidOperationException(
                "Persisted binary migration value has an unsupported data representation.")
        };
    }

    private static byte[] ReadBinary(
        DbDataReader reader,
        int ordinal)
    {
        var value = reader.GetValue(ordinal);

        return ReadBinary(value);
    }

    private static string BuildSelectSql(
        HiveMigrationTableDefinition table,
        bool sqlite) =>
        $"SELECT {string.Join(", ", table.Columns.Select(static c => $"[{c.Name}]"))} " +
        $"FROM {QuoteTable(table.Name, sqlite)} " +
        $"ORDER BY {string.Join(", ", table.OrderByColumns.Select(static c => $"[{c}]"))};";

    private static string BuildInsertSql(
        HiveMigrationTableDefinition table,
        bool sqlite) =>
        BuildInsertSql(
            table.Name,
            table.Columns.Select(static c => c.Name).ToArray(),
            sqlite);

    private static string BuildInsertSql(
        string tableName,
        IReadOnlyList<string> columns,
        bool sqlite) =>
        $"INSERT INTO {QuoteTable(tableName, sqlite)} " +
        $"({string.Join(", ", columns.Select(static c => $"[{c}]"))}) " +
        $"VALUES ({string.Join(", ", Enumerable.Range(0, columns.Count).Select(static i => $"@p{i}"))});";

    private static string QuoteTable(
        string tableName,
        bool sqlite) =>
        sqlite
            ? $"[{tableName}]"
            : $"[dbo].[{tableName}]";

    private static void AddParameters(
        DbCommand command,
        IReadOnlyList<HiveMigrationColumnDefinition> columns)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = $"@p{i}";
            parameter.DbType = columns[i].Kind switch
            {
                HiveMigrationColumnKind.Guid => DbType.Guid,
                HiveMigrationColumnKind.String => DbType.String,
                HiveMigrationColumnKind.Integer => DbType.Int64,
                HiveMigrationColumnKind.UtcDateTime => DbType.DateTime,
                HiveMigrationColumnKind.Binary => DbType.Binary,
                _ => throw new InvalidOperationException(
                    $"Unsupported migration column kind: {columns[i].Kind}.")
            };

            if (columns[i].Kind is HiveMigrationColumnKind.String or HiveMigrationColumnKind.Binary)
                parameter.Size = -1;

            if (parameter is SqliteParameter sqliteParameter)
            {
                sqliteParameter.SqliteType = columns[i].Kind switch
                {
                    HiveMigrationColumnKind.Guid or HiveMigrationColumnKind.String or HiveMigrationColumnKind.UtcDateTime
                        => SqliteType.Text,
                    HiveMigrationColumnKind.Integer => SqliteType.Integer,
                    HiveMigrationColumnKind.Binary => SqliteType.Blob,
                    _ => throw new InvalidOperationException(
                        $"Unsupported SQLite migration column kind: {columns[i].Kind}.")
                };
            }

            command.Parameters.Add(parameter);
        }
    }

    private static void BindParameters(
        DbCommand command,
        IReadOnlyList<HiveMigrationColumnDefinition> columns,
        IReadOnlyList<object?> values)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            var parameter = (DbParameter)command.Parameters[i];
            var value = values[i];

            if (value is null)
            {
                parameter.Value = DBNull.Value;
                continue;
            }

            if (parameter is SqliteParameter)
            {
                parameter.Value = columns[i].Kind switch
                {
                    HiveMigrationColumnKind.Guid =>
                        ((Guid)value).ToString("D"),
                    HiveMigrationColumnKind.UtcDateTime =>
                        ((DateTimeOffset)value).UtcDateTime.ToString(
                            "O",
                            CultureInfo.InvariantCulture),
                    _ => value
                };

                continue;
            }

            parameter.Value = columns[i].Kind switch
            {
                HiveMigrationColumnKind.UtcDateTime =>
                    ((DateTimeOffset)value).UtcDateTime,
                _ => value
            };
        }
    }

    private async Task<Result> PrepareEmbeddedDestinationAsync(
        EmbeddedPersistenceDatabase destination,
        CancellationToken cancellationToken)
    {
        var status = await destination.InspectAsync(
            cancellationToken).ConfigureAwait(false);

        if (status.IsFailure)
            return Result.Failure(status.Error!);

        if (status.Value!.DatabaseState == HiveDatabaseState.DatabaseNotFound)
        {
            var initialized = await destination.InitializeAsync(
                cancellationToken).ConfigureAwait(false);

            return initialized.IsFailure
                ? Result.Failure(initialized.Error!)
                : await EnsureEmbeddedDestinationEmptyAsync(
                    destination,
                    cancellationToken).ConfigureAwait(false);
        }

        if (status.Value.DatabaseState is HiveDatabaseState.NeedsMigration or HiveDatabaseState.FutureSchema)
        {
            return Result.Failure(
                Error.Unsupported(
                    "hive.persistence.data-migration.destination-schema-incompatible",
                    "The destination Embedded database exists but is not at the current supported schema version."));
        }

        if (status.Value.DatabaseState == HiveDatabaseState.SchemaNotInitialized)
        {
            var initialized = await destination.InitializeAsync(
                cancellationToken).ConfigureAwait(false);

            if (initialized.IsFailure)
                return Result.Failure(initialized.Error!);
        }

        var shape = await ValidateEmbeddedDatabaseShapeAsync(
            destination,
            cancellationToken).ConfigureAwait(false);

        if (shape.IsFailure)
            return Result.Failure(shape.Error!);

        return await EnsureEmbeddedDestinationEmptyAsync(
            destination,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> ValidateEmbeddedDatabaseShapeAsync(
        EmbeddedPersistenceDatabase database,
        CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenConnectionAsync(
            cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT [name]
            FROM [sqlite_master]
            WHERE [type] = 'table'
              AND [name] NOT LIKE 'sqlite_%'
            ORDER BY [name];
            """;

        var actual = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            actual.Add(reader.GetString(0));

        var expected = Tables
            .Select(static table => table.Name)
            .Append(EmbeddedPersistenceSchema.SchemaVersionTableName)
            .Append(EmbeddedPersistenceSchema.MigrationJournalTableName)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            return Result.Failure(
                Error.Conflict(
                    "hive.persistence.data-migration.embedded-schema-incompatible",
                    "The Embedded persistence database does not contain exactly the supported Hive schema tables."));
        }

        return Result.Success();
    }

    private async Task<Result> EnsureEmbeddedDestinationEmptyAsync(
        EmbeddedPersistenceDatabase database,
        CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenConnectionAsync(
            cancellationToken).ConfigureAwait(false);

        var empty = await IsDatabaseEmptyAsync(
            connection,
            sqlite: true,
            cancellationToken).ConfigureAwait(false);

        return empty
            ? Result.Success()
            : Result.Failure(
                Error.Conflict(
                    "hive.persistence.data-migration.destination-not-empty",
                    "The destination Embedded database already contains Hive data."));
    }

    private async Task<Result> PrepareSqlDestinationAsync(
        HiveDatabaseOptions options,
        CancellationToken cancellationToken)
    {
        var exists = await SqlDatabaseExistsAsync(
            options,
            cancellationToken).ConfigureAwait(false);

        if (!exists)
        {
            if (!options.CreateDatabaseIfMissing)
            {
                return Result.Failure(
                    new Error(
                        "hive.persistence.data-migration.destination-not-found",
                        ErrorCategory.NotFound,
                        "The destination SQL Server database does not exist."));
            }

            var createOptions = new HiveDatabaseOptions(
                options.ConnectionString,
                createDatabaseIfMissing: true,
                commandTimeoutSeconds: options.CommandTimeoutSeconds);

            var migration = await new HiveDatabaseMigrator(
                createOptions).MigrateAsync(cancellationToken).ConfigureAwait(false);

            return migration.IsFailure
                ? Result.Failure(migration.Error!)
                : Result.Success();
        }

        var inspection = await InspectSqlDatabaseAsync(
            options,
            cancellationToken).ConfigureAwait(false);

        if (inspection.IsFailure)
        {
            if (inspection.Error!.Code ==
                "hive.persistence.data-migration.schema-not-initialized")
            {
                var initialize = await new HiveDatabaseMigrator(
                    new HiveDatabaseOptions(
                        options.ConnectionString,
                        createDatabaseIfMissing: true,
                        commandTimeoutSeconds: options.CommandTimeoutSeconds))
                    .MigrateAsync(cancellationToken).ConfigureAwait(false);

                if (initialize.IsFailure)
                    return Result.Failure(initialize.Error!);
            }
            else
            {
                return Result.Failure(inspection.Error!);
            }
        }

        var empty = await IsSqlDatabaseEmptyAsync(
            options,
            cancellationToken).ConfigureAwait(false);

        return empty
            ? Result.Success()
            : Result.Failure(
                Error.Conflict(
                    "hive.persistence.data-migration.destination-not-empty",
                    "The destination SQL Server database already contains Hive data."));
    }

    private async Task<Result<SqlDatabaseInspection>> InspectSqlDatabaseAsync(
        HiveDatabaseOptions options,
        CancellationToken cancellationToken)
    {
        if (!await SqlDatabaseExistsAsync(
                options,
                cancellationToken).ConfigureAwait(false))
        {
            return Result<SqlDatabaseInspection>.Failure(
                new Error(
                    "hive.persistence.data-migration.source-not-found",
                    ErrorCategory.NotFound,
                    "The source SQL Server database does not exist."));
        }

        await using var connection = new SqlConnection(
            options.ConnectionString);

        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var tables = await ReadSqlUserTablesAsync(
            connection,
            options.CommandTimeoutSeconds,
            cancellationToken).ConfigureAwait(false);

        var allowed = new HashSet<string>(
            Tables.Select(static t => t.Name)
                .Append(HiveDatabaseSchema.SchemaVersionTableName)
                .Append(HiveDatabaseSchema.MigrationJournalTableName),
            StringComparer.OrdinalIgnoreCase);

        var unexpected = tables
            .Where(table => !allowed.Contains(table))
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

        if (unexpected.Length != 0)
        {
            return Result<SqlDatabaseInspection>.Failure(
                Error.Conflict(
                    "hive.persistence.data-migration.schema-incompatible",
                    "The SQL Server database contains tables outside the Hive persistence schema."));
        }

        var hasSchemaTable = tables.Contains(
            HiveDatabaseSchema.SchemaVersionTableName,
            StringComparer.OrdinalIgnoreCase);
        var hasJournalTable = tables.Contains(
            HiveDatabaseSchema.MigrationJournalTableName,
            StringComparer.OrdinalIgnoreCase);

        if (!hasSchemaTable && !hasJournalTable)
        {
            return Result<SqlDatabaseInspection>.Failure(
                Error.Conflict(
                    "hive.persistence.data-migration.schema-not-initialized",
                    "The SQL Server database does not have initialized Hive schema metadata."));
        }

        if (!hasSchemaTable || !hasJournalTable)
        {
            return Result<SqlDatabaseInspection>.Failure(
                Error.Conflict(
                    "hive.persistence.data-migration.schema-incomplete",
                    "The SQL Server Hive schema metadata is incomplete."));
        }

        var schemaVersion = await ReadSqlSchemaVersionAsync(
            connection,
            options.CommandTimeoutSeconds,
            cancellationToken).ConfigureAwait(false);

        if (schemaVersion is null)
        {
            return Result<SqlDatabaseInspection>.Failure(
                Error.Conflict(
                    "hive.persistence.data-migration.schema-incomplete",
                    "The SQL Server Hive schema version row is missing."));
        }

        if (schemaVersion != CurrentSchemaVersion)
        {
            return Result<SqlDatabaseInspection>.Failure(
                Error.Unsupported(
                    "hive.persistence.data-migration.source-schema-incompatible",
                    $"The SQL Server Hive schema is version {schemaVersion}, but version {CurrentSchemaVersion} is required."));
        }

        var missing = Tables
            .Select(static table => table.Name)
            .Where(table => !tables.Contains(table, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (missing.Length != 0)
        {
            return Result<SqlDatabaseInspection>.Failure(
                Error.Conflict(
                    "hive.persistence.data-migration.schema-incomplete",
                    "The SQL Server Hive schema is incomplete."));
        }

        var journalCount = await ReadSqlMigrationJournalCountAsync(
            connection,
            options.CommandTimeoutSeconds,
            cancellationToken).ConfigureAwait(false);

        if (journalCount != CurrentSchemaVersion)
        {
            return Result<SqlDatabaseInspection>.Failure(
                Error.Conflict(
                    "hive.persistence.data-migration.schema-incomplete",
                    "The SQL Server Hive migration journal does not match the current schema version."));
        }

        return Result<SqlDatabaseInspection>.Success(
            new SqlDatabaseInspection(
                schemaVersion.Value,
                tables));
    }

    private async Task<bool> SqlDatabaseExistsAsync(
        HiveDatabaseOptions options,
        CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(
            options.ConnectionString)
        {
            InitialCatalog = "master"
        };

        await using var connection = new SqlConnection(
            builder.ConnectionString);

        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandTimeout = options.CommandTimeoutSeconds;
        command.CommandText = """
            SELECT COUNT_BIG(1)
            FROM [sys].[databases]
            WHERE [name] = @DatabaseName;
            """;
        command.Parameters.Add(
            new SqlParameter(
                "@DatabaseName",
                SqlDbType.NVarChar,
                128)
            {
                Value = options.DatabaseName
            });

        return Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture) == 1;
    }

    private static async Task<string[]> ReadSqlUserTablesAsync(
        SqlConnection connection,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = commandTimeoutSeconds;
        command.CommandText = """
            SELECT [t].[name]
            FROM [sys].[tables] AS [t]
            WHERE [t].[is_ms_shipped] = 0
              AND [t].[schema_id] = SCHEMA_ID(N'dbo')
            ORDER BY [t].[name];
            """;

        var names = new List<string>();

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            names.Add(reader.GetString(0));

        return names.ToArray();
    }

    private static async Task<int?> ReadSqlSchemaVersionAsync(
        SqlConnection connection,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = commandTimeoutSeconds;
        command.CommandText = """
            IF OBJECT_ID(N'[dbo].[HiveSchemaVersion]', N'U') IS NULL
                SELECT CAST(NULL AS int);
            ELSE
                SELECT [SchemaVersion]
                FROM [dbo].[HiveSchemaVersion]
                WHERE [SchemaRowId] = @SchemaRowId;
            """;
        command.Parameters.Add(
            new SqlParameter(
                "@SchemaRowId",
                SqlDbType.TinyInt)
            {
                Value = HiveDatabaseSchema.SchemaRowId
            });

        var value = await command.ExecuteScalarAsync(
            cancellationToken).ConfigureAwait(false);

        return value is null or DBNull
            ? null
            : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static async Task<long> ReadSqlMigrationJournalCountAsync(
        SqlConnection connection,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = commandTimeoutSeconds;
        command.CommandText = """
            IF OBJECT_ID(N'[dbo].[HiveMigrationJournal]', N'U') IS NULL
                SELECT CAST(0 AS bigint);
            ELSE
                SELECT COUNT_BIG(1)
                FROM [dbo].[HiveMigrationJournal];
            """;

        return Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture);
    }

    private async Task<bool> IsSqlDatabaseEmptyAsync(
        HiveDatabaseOptions options,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(
            options.ConnectionString);

        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        foreach (var table in Tables)
        {
            await using var command = connection.CreateCommand();
            command.CommandTimeout = options.CommandTimeoutSeconds;
            command.CommandText = $"SELECT COUNT_BIG(1) FROM [dbo].[{table.Name}];";

            if (Convert.ToInt64(
                    await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
                    CultureInfo.InvariantCulture) != 0)
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> IsDatabaseEmptyAsync(
        DbConnection connection,
        bool sqlite,
        CancellationToken cancellationToken)
    {
        foreach (var table in Tables)
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"SELECT COUNT(*) FROM {QuoteTable(table.Name, sqlite)};";

            if (Convert.ToInt64(
                    await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
                    CultureInfo.InvariantCulture) != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static Error MigrationIncompatibleSource(
        HiveDatabaseState state,
        int? version) =>
        state == HiveDatabaseState.DatabaseNotFound
            ? new Error(
                "hive.persistence.data-migration.source-not-found",
                ErrorCategory.NotFound,
                "The source Embedded database does not exist.")
            : Error.Unsupported(
                "hive.persistence.data-migration.source-schema-incompatible",
                state switch
                {
                    HiveDatabaseState.FutureSchema =>
                        $"The source Embedded schema is version {version}, which is newer than this Hive build supports.",
                    HiveDatabaseState.NeedsMigration =>
                        $"The source Embedded schema is version {version}, but version {CurrentSchemaVersion} is required.",
                    _ =>
                        "The source Embedded persistence schema is not initialized or is incomplete."
                });

    private sealed record SqlDatabaseInspection(
        int SchemaVersion,
        IReadOnlyList<string> Tables);
}

internal enum HiveMigrationColumnKind
{
    Guid,
    String,
    Integer,
    UtcDateTime,
    Binary
}

internal sealed record HiveMigrationColumnDefinition(
    string Name,
    HiveMigrationColumnKind Kind,
    bool Nullable = false);

internal sealed record HiveMigrationTableDefinition(
    string Name,
    IReadOnlyList<HiveMigrationColumnDefinition> Columns,
    params string[] OrderByColumns);

internal sealed class HiveMigrationFingerprintSet : IDisposable
{
    private sealed class Fingerprint
    {
        public Fingerprint()
        {
            Hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        }

        public IncrementalHash Hash { get; }

        public long Count { get; set; }

        public byte[] GetDigest() =>
            Digest ??= Hash.GetHashAndReset();

        private byte[]? Digest { get; set; }
    }

    private readonly Dictionary<string, Fingerprint> _fingerprints =
        new(StringComparer.Ordinal);

    public void AddRow(
        string tableName,
        IReadOnlyList<HiveMigrationColumnDefinition> columns,
        IReadOnlyList<object?> values,
        string? secretMaterial = null)
    {
        if (!_fingerprints.TryGetValue(tableName, out var fingerprint))
        {
            fingerprint = new Fingerprint();
            _fingerprints.Add(tableName, fingerprint);
        }

        for (var i = 0; i < columns.Count; i++)
            AppendValue(fingerprint.Hash, columns[i].Kind, values[i]);

        if (secretMaterial is not null)
            AppendBytes(
                fingerprint.Hash,
                Encoding.UTF8.GetBytes(secretMaterial));

        fingerprint.Count++;
    }

    public bool Matches(HiveMigrationFingerprintSet other)
    {
        foreach (var (tableName, fingerprint) in _fingerprints)
        {
            if (!other._fingerprints.TryGetValue(
                    tableName,
                    out var otherFingerprint) ||
                fingerprint.Count != otherFingerprint.Count)
            {
                return false;
            }

            var left = fingerprint.GetDigest();
            var right = otherFingerprint.GetDigest();

            if (!CryptographicOperations.FixedTimeEquals(left, right))
                return false;
        }

        return _fingerprints.Count == other._fingerprints.Count;
    }

    public void Dispose()
    {
        foreach (var fingerprint in _fingerprints.Values)
            fingerprint.Hash.Dispose();

        _fingerprints.Clear();
    }

    private static void AppendValue(
        IncrementalHash hash,
        HiveMigrationColumnKind kind,
        object? value)
    {
        AppendString(
            hash,
            kind.ToString());

        if (value is null)
        {
            AppendString(hash, "<null>");
            return;
        }

        switch (kind)
        {
            case HiveMigrationColumnKind.Guid:
                AppendString(
                    hash,
                    ((Guid)value).ToString("D"));
                break;

            case HiveMigrationColumnKind.String:
                AppendString(
                    hash,
                    (string)value);
                break;

            case HiveMigrationColumnKind.Integer:
                AppendString(
                    hash,
                    ((long)value).ToString(
                        CultureInfo.InvariantCulture));
                break;

            case HiveMigrationColumnKind.UtcDateTime:
                AppendString(
                    hash,
                    ((DateTimeOffset)value).UtcDateTime.ToString(
                        "O",
                        CultureInfo.InvariantCulture));
                break;

            case HiveMigrationColumnKind.Binary:
                AppendBytes(
                    hash,
                    (byte[])value);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported migration column kind: {kind}.");
        }
    }

    private static void AppendString(
        IncrementalHash hash,
        string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        AppendBytes(hash, bytes);
    }

    private static void AppendBytes(
        IncrementalHash hash,
        ReadOnlySpan<byte> bytes)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }
}

internal static class DpapiSecretProtection
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static byte[] Protect(SecretMaterial material)
    {
        ArgumentNullException.ThrowIfNull(material);

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Hive DPAPI secret storage requires Windows.");
        }

        var plaintext = Encoding.UTF8.GetBytes(material.Reveal());

        try
        {
            return ProtectedData.Protect(
                plaintext,
                null,
                DataProtectionScope.CurrentUser);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public static SecretMaterial Unprotect(byte[] encryptedValue)
    {
        ArgumentNullException.ThrowIfNull(encryptedValue);

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Hive DPAPI secret storage requires Windows.");
        }

        byte[] plaintext;

        try
        {
            plaintext = ProtectedData.Unprotect(
                encryptedValue,
                null,
                DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException exception)
        {
            throw new CryptographicException(
                "The stored secret could not be decrypted for the current Windows user.",
                exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encryptedValue);
        }

        try
        {
            return SecretMaterial.Create(
                StrictUtf8.GetString(plaintext));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }
}
