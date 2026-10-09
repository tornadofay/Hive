using Hive.Core;
using Hive.Persistence;

namespace Hive.Management;

internal sealed class HivePersistenceMigrationManagementService : HiveManagementServiceBase
{
    private readonly IHiveBootstrapCredentialStore? _bootstrapCredentials;
    private readonly IHivePersistenceMigrationQuiescence? _quiescence;
    private readonly HivePersistenceDataMigrator _migrator;

    public HivePersistenceMigrationManagementService(
        IHiveBootstrapCredentialStore? bootstrapCredentials,
        IHivePersistenceMigrationQuiescence? quiescence,
        HivePersistenceDataMigrator? migrator = null)
    {
        _bootstrapCredentials = bootstrapCredentials;
        _quiescence = quiescence;
        _migrator = migrator ?? new HivePersistenceDataMigrator();
    }

    public async Task<Result<HivePersistenceMigrationResult>> MigrateAsync(
        HivePersistenceMigrationRequest request,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.SourceConfiguration);
        ArgumentNullException.ThrowIfNull(request.DestinationConfiguration);

        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return Result<HivePersistenceMigrationResult>.Failure(contextError);

        if (_quiescence is null)
        {
            return Result<HivePersistenceMigrationResult>.Failure(
                Error.Unsupported(
                    "hive.management.persistence-migration-quiescence-unavailable",
                    "Hive persistence migration cannot proceed because the active service graph cannot be quiesced."));
        }

        var source = request.SourceConfiguration;
        var destination = request.DestinationConfiguration;

        if (source.Backend == destination.Backend)
        {
            return Result<HivePersistenceMigrationResult>.Failure(
                Error.Validation(
                    "hive.management.persistence-migration.backend-direction-invalid",
                    "Persistence migration requires the destination to use the other Hive persistence backend."));
        }

        var leaseResult = await _quiescence
            .AcquireAsync(cancellationToken)
            .ConfigureAwait(false);

        if (leaseResult.IsFailure)
            return Result<HivePersistenceMigrationResult>.Failure(
                SanitizeTechnicalError(
                    leaseResult.Error!,
                    "The Hive persistence service graph could not be quiesced for migration."));

        if (leaseResult.Value is null)
        {
            return Result<HivePersistenceMigrationResult>.Failure(
                new Error(
                    "hive.management.persistence-migration.quiescence-invalid",
                    ErrorCategory.Internal,
                    "The persistence migration quiescence boundary returned no lease."));
        }

        await using var lease = leaseResult.Value;

        SecretMaterial? sourceCredential = null;
        SecretMaterial? destinationCredential = null;

        try
        {
            var sourceCredentialResult = await ResolveSqlCredentialAsync(
                source,
                cancellationToken).ConfigureAwait(false);

            if (sourceCredentialResult.IsFailure)
                return Result<HivePersistenceMigrationResult>.Failure(
                    sourceCredentialResult.Error!);

            sourceCredential = sourceCredentialResult.Value;

            var destinationCredentialResult = await ResolveSqlCredentialAsync(
                destination,
                cancellationToken).ConfigureAwait(false);

            if (destinationCredentialResult.IsFailure)
                return Result<HivePersistenceMigrationResult>.Failure(
                    destinationCredentialResult.Error!);

            destinationCredential = destinationCredentialResult.Value;

            var migrationId = Guid.NewGuid();

            var execution = await _migrator
                .MigrateAsync(
                    source,
                    sourceCredential,
                    destination,
                    destinationCredential,
                    migrationId,
                    accessContext,
                    cancellationToken)
                .ConfigureAwait(false);

            if (execution.IsFailure)
            {
                var executionError = execution.Error!;
                var safeMessage = executionError.Code ==
                    "hive.persistence.data-migration.sql-failure"
                        // The persistence migrator constructs this diagnostic from a
                        // fixed SQL-error mapping and endpoint identity. It omits raw
                        // provider text, connection strings, and credentials.
                        ? executionError.Message
                        : "Hive persistence data migration could not be completed safely.";

                return Result<HivePersistenceMigrationResult>.Failure(
                    SanitizeTechnicalError(
                        executionError,
                        safeMessage));
            }

            var evidence = execution.Value
                ?? throw new InvalidOperationException(
                    "The persistence migration returned no execution result.");

            var direction = source.Backend == HivePersistenceBackend.SqlServer
                ? HivePersistenceMigrationDirection.SqlServerToEmbedded
                : HivePersistenceMigrationDirection.EmbeddedToSqlServer;

            return Result<HivePersistenceMigrationResult>.Success(
                new HivePersistenceMigrationResult(
                    evidence.MigrationId,
                    direction,
                    evidence.SourceBackend,
                    evidence.DestinationBackend,
                    evidence.SourceSchemaVersion,
                    evidence.DestinationSchemaVersion,
                    evidence.TotalRecordsMigrated,
                    evidence.RecordCounts,
                    evidence.SourceVerifiedUnchanged,
                    evidence.DestinationVerified,
                    DestinationActivated: false));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ArgumentException)
        {
            return Result<HivePersistenceMigrationResult>.Failure(
                Error.Validation(
                    "hive.management.persistence-migration.configuration-invalid",
                    "The persistence migration configuration is invalid."));
        }
        catch (PlatformNotSupportedException)
        {
            return Result<HivePersistenceMigrationResult>.Failure(
                Error.Unsupported(
                    "hive.management.persistence-migration.platform-unsupported",
                    "The configured persistence migration requires a supported operating-system security facility."));
        }
        catch (Exception)
        {
            return Result<HivePersistenceMigrationResult>.Failure(
                new Error(
                    "hive.management.persistence-migration.failed",
                    ErrorCategory.Internal,
                    "Hive persistence data migration failed unexpectedly."));
        }
        finally
        {
            destinationCredential?.Dispose();
            sourceCredential?.Dispose();
        }
    }

    private async Task<Result<SecretMaterial?>> ResolveSqlCredentialAsync(
        HivePersistenceConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (configuration.Backend != HivePersistenceBackend.SqlServer ||
            configuration.AuthenticationMode != HiveSqlAuthenticationMode.SqlPassword)
        {
            return Result<SecretMaterial?>.Success(null);
        }

        if (configuration.BootstrapCredential is null ||
            _bootstrapCredentials is null)
        {
            return Result<SecretMaterial?>.Failure(
                Error.Validation(
                    "hive.management.persistence-migration.bootstrap-credential-required",
                    "SQL password authentication requires a configured bootstrap credential for the migration source or destination."));
        }

        var credential = await _bootstrapCredentials
            .ResolveAsync(
                configuration.BootstrapCredential.Value,
                cancellationToken)
            .ConfigureAwait(false);

        if (credential.IsFailure)
        {
            return Result<SecretMaterial?>.Failure(
                SanitizeTechnicalError(
                    credential.Error!,
                    "The SQL Server bootstrap credential could not be resolved for migration."));
        }

        return Result<SecretMaterial?>.Success(credential.Value);
    }
}
