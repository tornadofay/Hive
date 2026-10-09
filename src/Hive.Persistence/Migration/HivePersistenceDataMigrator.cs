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

internal sealed class HivePersistenceDataMigrator
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
        ResourceAccessContext accessContext,
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
                    accessContext,
                    cancellationToken).ConfigureAwait(false)
                : await MigrateEmbeddedToSqlAsync(
                    sourceConfiguration,
                    destinationSqlCredential,
                    destinationConfiguration,
                    migrationId,
                    accessContext,
                    cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HiveSecretMigrationResolutionException exception)
        {
            return Result<HivePersistenceMigrationExecutionResult>.Failure(
                exception.Error);
        }
        catch (SqlException exception)
        {
            return Result<HivePersistenceMigrationExecutionResult>.Failure(
                HivePersistenceError.External(
                    "hive.persistence.data-migration.sql-failure",
                    DescribeSqlMigrationFailure(
                        sourceConfiguration,
                        destinationConfiguration,
                        GetSqlErrorNumber(exception),
                        GetSqlErrorState(exception),
                        GetSqlErrorClass(exception)),
                    exception));
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

    internal static string DescribeSqlMigrationFailure(
        HivePersistenceConfiguration sourceConfiguration,
        HivePersistenceConfiguration destinationConfiguration,
        int errorNumber,
        byte state,
        byte errorClass)
    {
        ArgumentNullException.ThrowIfNull(sourceConfiguration);
        ArgumentNullException.ThrowIfNull(destinationConfiguration);

        var sqlConfiguration = sourceConfiguration.Backend == HivePersistenceBackend.SqlServer
            ? sourceConfiguration
            : destinationConfiguration;
        var endpointRole = destinationConfiguration.Backend == HivePersistenceBackend.SqlServer
            ? "destination"
            : "source";

        var guidance = errorNumber switch
        {
            911 =>
                "SQL Server could not find the configured database. If the database was renamed, verify that this endpoint uses its exact current name.",
            4060 or 916 =>
                "SQL Server reached the instance but could not open the configured database. Verify its name and the migration account's database access, especially if the database was renamed.",
            18456 =>
                "SQL Server rejected authentication. Verify Windows-integrated access or the configured SQL login without sharing credential material.",
            1801 =>
                "SQL Server reports that a database with this name already exists. Check the selected destination name and whether the intended destination is already present.",
            229 or 262 =>
                "SQL Server denied a required operation. The migration account may need database creation, schema initialization, and read/write permissions for the selected migration direction.",
            2627 or 2601 =>
                "SQL Server rejected a duplicate key during transfer. The destination must be empty and compatible; do not retry against an existing Hive dataset.",
            2714 =>
                "A required schema object already exists while preparing the SQL Server database. Inspect the destination state before retrying; do not drop a database that may contain useful Hive data.",
            -2 =>
                "The SQL Server operation timed out. Verify server responsiveness and the configured command timeout.",
            -1 or 2 or 26 or 40 or 53 or 11001 or 11004 =>
                "The SQL Server endpoint could not be reached. Verify the instance name, SQL Server service, and named-instance or port resolution.",
            _ =>
                "Verify the SQL Server endpoint and database name, database permissions, and schema compatibility for the selected migration direction."
        };

        return $"The {endpointRole} SQL Server endpoint '{sqlConfiguration.ServerName}', database '{sqlConfiguration.DatabaseName}' failed during migration (SQL error {errorNumber}, state {state}, class {errorClass}). {guidance} Raw SQL error text, connection strings, and credentials were omitted from this diagnostic.";
    }

    private static int GetSqlErrorNumber(SqlException exception)
    {
        var firstError = exception.Errors
            .Cast<SqlError>()
            .FirstOrDefault(static error => error.Number != 0)
            ?? (exception.Errors.Count > 0 ? exception.Errors[0] : null);
        return firstError?.Number ?? exception.Number;
    }

    private static byte GetSqlErrorState(SqlException exception)
    {
        var firstError = exception.Errors
            .Cast<SqlError>()
            .FirstOrDefault(static error => error.Number != 0)
            ?? (exception.Errors.Count > 0 ? exception.Errors[0] : null);
        return firstError?.State ?? 0;
    }

    private static byte GetSqlErrorClass(SqlException exception)
    {
        var firstError = exception.Errors
            .Cast<SqlError>()
            .FirstOrDefault(static error => error.Number != 0)
            ?? (exception.Errors.Count > 0 ? exception.Errors[0] : null);
        return firstError?.Class ?? 0;
    }

    private async Task<Result<HivePersistenceMigrationExecutionResult>> MigrateSqlToEmbeddedAsync(
        HivePersistenceConfiguration sourceConfiguration,
        SecretMaterial? sourceSqlCredential,
        HivePersistenceConfiguration destinationConfiguration,
        Guid migrationId,
        ResourceAccessContext accessContext,
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

        var sourceSecretStore = new SqlDpapiSecretStore(sourceOptions);
        var destinationSecretWriter =
            (IHiveSecretStoreMigrationWriter)new EmbeddedDpapiSecretStore(destination);

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
            sourceSecretStore,
            destinationSecretWriter,
            accessContext,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<HivePersistenceMigrationExecutionResult>> MigrateEmbeddedToSqlAsync(
        HivePersistenceConfiguration sourceConfiguration,
        SecretMaterial? destinationSqlCredential,
        HivePersistenceConfiguration destinationConfiguration,
        Guid migrationId,
        ResourceAccessContext accessContext,
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
                MigrationIncompatibleSource(
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
                destinationSqlCredential);

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

        var sourceSecretStore = new EmbeddedDpapiSecretStore(source);
        var destinationSecretWriter =
            (IHiveSecretStoreMigrationWriter)new SqlDpapiSecretStore(destinationOptions);

        return await TransferAndVerifyAsync(
            migrationId,
            HivePersistenceBackend.Embedded,
            HivePersistenceBackend.SqlServer,
            sourceConnection,
            destinationConnection,
            sourceSecretStore,
            destinationSecretWriter,
            accessContext,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<HivePersistenceMigrationExecutionResult>> TransferAndVerifyAsync(
        Guid migrationId,
        HivePersistenceBackend sourceBackend,
        HivePersistenceBackend destinationBackend,
        DbConnection sourceConnection,
        object destinationDatabase,
        ISecretStore sourceSecretStore,
        IHiveSecretStoreMigrationWriter destinationSecretWriter,
        ResourceAccessContext accessContext,
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
                sourceSecretStore,
                destinationSecretWriter,
                accessContext,
                cancellationToken).ConfigureAwait(false);
        }

        var sqlDestination = (SqlConnection)destinationDatabase;
        return await TransferAndVerifyCoreAsync(
            migrationId,
            sourceBackend,
            destinationBackend,
            sourceConnection,
            sqlDestination,
            sourceSecretStore,
            destinationSecretWriter,
            accessContext,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<HivePersistenceMigrationExecutionResult>> TransferAndVerifyCoreAsync(
        Guid migrationId,
        HivePersistenceBackend sourceBackend,
        HivePersistenceBackend destinationBackend,
        DbConnection sourceConnection,
        DbConnection destinationConnection,
        ISecretStore sourceSecretStore,
        IHiveSecretStoreMigrationWriter destinationSecretWriter,
        ResourceAccessContext accessContext,
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
                var secretTransfer = await TransferSecretsAsync(
                    sourceConnection,
                    destinationConnection,
                    transaction,
                    table,
                    sourceSecretStore,
                    destinationSecretWriter,
                    accessContext,
                    sourceFingerprint,
                    rowCounts,
                    cancellationToken).ConfigureAwait(false);

                if (secretTransfer.IsFailure)
                {
                    await transaction.RollbackAsync(
                        CancellationToken.None).ConfigureAwait(false);

                    return Result<HivePersistenceMigrationExecutionResult>.Failure(
                        secretTransfer.Error!);
                }
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
            null,
            accessContext,
            cancellationToken).ConfigureAwait(false);

        if (!sourceFingerprint.Matches(destinationFingerprint))
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            return Result<HivePersistenceMigrationExecutionResult>.Failure(
                Error.Conflict(
                    "hive.persistence.data-migration.verification-failed",
                    "The migration destination does not exactly match the source logical dataset."));
        }

        using var sourceAfter = await ReadFingerprintAsync(
            sourceConnection,
            sourceBackend,
            null,
            sourceSecretStore,
            accessContext,
            cancellationToken).ConfigureAwait(false);

        if (!sourceFingerprint.Matches(sourceAfter))
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
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
        AddParameters(destinationCommand, table.Columns);

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

    private async Task<Result> TransferSecretsAsync(
        DbConnection sourceConnection,
        DbConnection destinationConnection,
        DbTransaction destinationTransaction,
        HiveMigrationTableDefinition table,
        ISecretStore sourceSecretStore,
        IHiveSecretStoreMigrationWriter destinationSecretWriter,
        ResourceAccessContext accessContext,
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

        long count = 0;

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var values = ReadRow(reader, table);

            if (values[0] is not Guid secretGuid)
            {
                return Result.Failure(
                    new Error(
                        "hive.persistence.data-migration.secret-identity-invalid",
                        ErrorCategory.Internal,
                        "A persisted SecretId has an invalid data representation."));
            }

            var secretResult = await sourceSecretStore.GetAsync(
                new SecretId(secretGuid),
                accessContext,
                cancellationToken).ConfigureAwait(false);

            if (secretResult.IsFailure)
                return Result.Failure(secretResult.Error!);

            var secretRead = secretResult.Value
                ?? throw new InvalidOperationException(
                    "The source Secret Store returned no secret material.");

            using var material = secretRead.Material;

            fingerprints.AddRow(
                table.Name,
                table.Columns,
                values,
                material.Reveal());

            var imported = await destinationSecretWriter.ImportForMigrationAsync(
                destinationConnection,
                destinationTransaction,
                secretRead.Secret,
                material,
                accessContext,
                cancellationToken).ConfigureAwait(false);

            if (imported.IsFailure)
                return imported;

            count++;
        }

        counts[table.Name] = count;
        return Result.Success();
    }

    private async Task<HiveMigrationFingerprintSet> ReadFingerprintAsync(
        DbConnection connection,
        HivePersistenceBackend backend,
        DbTransaction? transaction,
        ISecretStore? secretStore,
        ResourceAccessContext accessContext,
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
                    secretStore,
                    accessContext,
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
        ISecretStore? secretStore,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken)
    {
        if (transaction is null && secretStore is not null)
        {
            var select = BuildSelectSql(
                table,
                connection is SqliteConnection);

            await using var sourceCommand = connection.CreateCommand();
            sourceCommand.CommandText = select;

            await using var reader = await sourceCommand.ExecuteReaderAsync(
                CommandBehavior.SequentialAccess,
                cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var values = ReadRow(reader, table);

                if (values[0] is not Guid secretGuid)
                {
                    throw new InvalidOperationException(
                        "Persisted SecretId has an invalid data representation.");
                }

                var secretResult = await secretStore.GetAsync(
                    new SecretId(secretGuid),
                    accessContext,
                    cancellationToken).ConfigureAwait(false);

                if (secretResult.IsFailure)
                    throw new HiveSecretMigrationResolutionException(
                        secretResult.Error!);

                var secretRead = secretResult.Value
                    ?? throw new InvalidOperationException(
                        "The source Secret Store returned no secret material.");

                using var material = secretRead.Material;

                set.AddRow(
                    table.Name,
                    table.Columns,
                    values,
                    material.Reveal());
            }

            return;
        }

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
            table.OrderByColumns.Select(
                column => BuildOrderByExpression(
                    table,
                    column,
                    connection is SqliteConnection)));

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT {selectColumns} FROM {qualifiedTable} ORDER BY {orderBy};";

        await using var secretReader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        while (await secretReader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var values = ReadRow(secretReader, table);
            var encrypted = ReadBinary(
                secretReader,
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
        bool sqlite)
    {
        var orderBy = string.Join(
            ", ",
            table.OrderByColumns.Select(
                column => BuildOrderByExpression(
                    table,
                    column,
                    sqlite)));

        return $"SELECT {string.Join(", ", table.Columns.Select(static c => $"[{c.Name}]"))} " +
            $"FROM {QuoteTable(table.Name, sqlite)} " +
            $"ORDER BY {orderBy};";
    }

    private static string BuildOrderByExpression(
        HiveMigrationTableDefinition table,
        string column,
        bool sqlite)
    {
        var definition = table.Columns.FirstOrDefault(
            candidate => string.Equals(
                candidate.Name,
                column,
                StringComparison.Ordinal));

        if (definition is null)
        {
            throw new InvalidOperationException(
                $"Migration ordering column '{column}' is not defined on table '{table.Name}'.");
        }

        if (!sqlite && definition.Kind == HiveMigrationColumnKind.Guid)
            return $"CONVERT(varchar(36), [{column}])";

        return $"[{column}]";
    }

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
                HiveMigrationColumnKind.UtcDateTime => DbType.DateTime2,
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

        if (status.Value.DatabaseState == HiveDatabaseState.FutureSchema)
        {
            var version = status.Value.SchemaVersion
                ?? throw new InvalidOperationException(
                    "Future-schema Embedded destination inspection returned no schema version.");

            return Result.Failure(
                EmbeddedPersistenceError.FutureSchema(version));
        }

        if (status.Value.DatabaseState == HiveDatabaseState.NeedsMigration)
        {
            return Result.Failure(
                Error.Unsupported(
                    "hive.persistence.data-migration.destination-schema-incompatible",
                    "The destination Embedded database exists but is not at the current supported schema version."));
        }

        if (status.Value.DatabaseState == HiveDatabaseState.SchemaNotInitialized)
        {
            var existingTables = await ReadEmbeddedUserTablesAsync(
                destination,
                cancellationToken).ConfigureAwait(false);

            if (existingTables.Count != 0)
            {
                return Result.Failure(
                    Error.Conflict(
                        "hive.persistence.data-migration.embedded-schema-incomplete",
                        "The destination Embedded database contains tables but its Hive schema metadata is not initialized."));
            }

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

    private async Task<IReadOnlyList<string>> ReadEmbeddedUserTablesAsync(
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

        var names = new List<string>();

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            names.Add(reader.GetString(0));

        return names;
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
            if (tables.Any(static table => Tables.Any(
                    expected => string.Equals(
                        expected.Name,
                        table,
                        StringComparison.OrdinalIgnoreCase))))
            {
                return Result<SqlDatabaseInspection>.Failure(
                    Error.Conflict(
                        "hive.persistence.data-migration.schema-incomplete",
                        "The SQL Server database contains Hive data but its schema metadata is incomplete."));
            }

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

    private sealed class HiveSecretMigrationResolutionException : Exception
    {
        public HiveSecretMigrationResolutionException(Error error) =>
            Error = error;

        public Error Error { get; }
    }

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
