using Hive.Core;

using Hive.Persistence;



namespace Hive.Management;



internal sealed class HiveConfigurationManagementService : HiveManagementServiceBase

{

    private readonly IHiveConfigurationStore? _configurationStore

    private readonly IHivePersistenceConnectionTester? _persistenceConnectionTester

    private readonly IHiveBootstrapCredentialStore? _bootstrapCredentials

    private readonly SemaphoreSlim _bootstrapConfigurationMutationGate



    internal HiveConfigurationManagementService(IHiveConfigurationStore? configurationStore, IHivePersistenceConnectionTester? persistenceConnectionTester, IHiveBootstrapCredentialStore? bootstrapCredentials)

    {

        _configurationStore = configurationStore;

        _persistenceConnectionTester = persistenceConnectionTester;

        _bootstrapCredentials = bootstrapCredentials;

        _bootstrapConfigurationMutationGate = new SemaphoreSlim(1, 1);

    }



    internal async Task<Result<HivePersistenceConfiguration>> GetPersistenceConfigurationAsync(
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return Result<HivePersistenceConfiguration>.Failure(contextError);

        if (_configurationStore is null)
        {
            return Result<HivePersistenceConfiguration>.Failure(
                Error.Unsupported(
                    "hive.management.configuration-store-unavailable",
                    "Hive persistence configuration storage is not configured."));
        }

        return await _configurationStore
            .LoadPersistenceConfigurationAsync(cancellationToken)
            .ConfigureAwait(false);
    }


    internal async Task<Result<HivePersistenceConfiguration>> SavePersistenceConfigurationAsync(
        HivePersistenceConfiguration configuration,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return Result<HivePersistenceConfiguration>.Failure(contextError);

        if (_configurationStore is null)
        {
            return Result<HivePersistenceConfiguration>.Failure(
                Error.Unsupported(
                    "hive.management.configuration-store-unavailable",
                    "Hive persistence configuration storage is not configured."));
        }

        await _bootstrapConfigurationMutationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            return await _configurationStore
                .SavePersistenceConfigurationAsync(
                    configuration,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _bootstrapConfigurationMutationGate.Release();
        }
    }


    internal async Task<Result<HiveBootstrapCredentialReference>> SaveBootstrapCredentialAsync(
        SecretMaterial material,
        HiveBootstrapCredentialReference? existingReference,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(material);

        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return Result<HiveBootstrapCredentialReference>.Failure(contextError);

        if (_bootstrapCredentials is null)
        {
            return Result<HiveBootstrapCredentialReference>.Failure(
                Error.Unsupported(
                    "hive.management.bootstrap-credential-store-unavailable",
                    "The Hive bootstrap credential store is not configured."));
        }

        await _bootstrapConfigurationMutationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var reference = existingReference ??
                new HiveBootstrapCredentialReference(SecretId.New());

            var result = await _bootstrapCredentials
                .SetAsync(reference, material, cancellationToken)
                .ConfigureAwait(false);

            return result.IsSuccess
                ? Result<HiveBootstrapCredentialReference>.Success(reference)
                : Result<HiveBootstrapCredentialReference>.Failure(
                    SanitizeTechnicalError(
                        result.Error!,
                        "The bootstrap credential could not be stored."));
        }
        finally
        {
            _bootstrapConfigurationMutationGate.Release();
        }
    }


    internal async Task<Result> RemoveBootstrapCredentialAsync(
        HiveBootstrapCredentialReference reference,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return Result.Failure(contextError);

        if (_bootstrapCredentials is null)
        {
            return Result.Failure(
                Error.Unsupported(
                    "hive.management.bootstrap-credential-store-unavailable",
                    "The Hive bootstrap credential store is not configured."));
        }

        if (_configurationStore is null)
        {
            return Result.Failure(
                Error.Unsupported(
                    "hive.management.configuration-store-unavailable",
                    "Hive persistence configuration storage is required to safely remove a bootstrap credential."));
        }

        await _bootstrapConfigurationMutationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var configuration = await _configurationStore
                .LoadPersistenceConfigurationAsync(cancellationToken)
                .ConfigureAwait(false);

            if (configuration.IsFailure)
                return Result.Failure(configuration.Error!);

            if (configuration.Value!.BootstrapCredential == reference)
            {
                return Result.Failure(
                    Error.Validation(
                        "hive.management.bootstrap-credential-still-referenced",
                        "The bootstrap credential cannot be removed while persistence configuration still references it."));
            }

            var result = await _bootstrapCredentials
                .ClearAsync(reference, cancellationToken)
                .ConfigureAwait(false);

            return result.IsSuccess
                ? result
                : Result.Failure(
                    SanitizeTechnicalError(
                        result.Error!,
                        "The bootstrap credential could not be removed."));
        }
        finally
        {
            _bootstrapConfigurationMutationGate.Release();
        }
    }


    internal async Task<Result<HivePersistenceConnectionTest>> TestPersistenceConnectionAsync(
        HivePersistenceConfiguration configuration,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return Result<HivePersistenceConnectionTest>.Failure(contextError);

        if (_persistenceConnectionTester is null)
        {
            return Result<HivePersistenceConnectionTest>.Failure(
                Error.Unsupported(
                    "hive.management.persistence-tester-unavailable",
                    "Hive persistence connection testing is not configured."));
        }

        SecretMaterial? material = null;

        try
        {
            if (configuration.AuthenticationMode == HiveSqlAuthenticationMode.SqlPassword)
            {
                if (configuration.BootstrapCredential is null || _bootstrapCredentials is null)
                {
                    return Result<HivePersistenceConnectionTest>.Failure(
                        Error.Validation(
                            "hive.management.persistence-bootstrap-credential-required",
                            "SQL password authentication requires a configured bootstrap credential."));
                }

                var secret = await _bootstrapCredentials
                    .ResolveAsync(
                        configuration.BootstrapCredential.Value,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (secret.IsFailure)
                {
                    return Result<HivePersistenceConnectionTest>.Failure(
                        SanitizeTechnicalError(
                            secret.Error!,
                            "The bootstrap credential could not be resolved."));
                }

                material = secret.Value!;
            }

            return await _persistenceConnectionTester
                .TestAsync(
                    configuration,
                    material,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            material?.Dispose();
        }
    }


    internal async Task<Result> InitializePersistenceAsync(
        HivePersistenceConfiguration configuration,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return Result.Failure(contextError);

        SecretMaterial? material = null;

        try
        {
            if (configuration.AuthenticationMode == HiveSqlAuthenticationMode.SqlPassword)
            {
                if (configuration.BootstrapCredential is null ||
                    _bootstrapCredentials is null)
                {
                    return Result.Failure(
                        Error.Validation(
                            "hive.management.persistence-bootstrap-credential-required",
                            "SQL password authentication requires a configured bootstrap credential."));
                }

                var secret = await _bootstrapCredentials
                    .ResolveAsync(
                        configuration.BootstrapCredential.Value,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (secret.IsFailure)
                {
                    return Result.Failure(
                        SanitizeTechnicalError(
                            secret.Error!,
                            "The bootstrap credential could not be resolved."));
                }

                material = secret.Value!;
            }

            HiveDatabaseOptions options;

            try
            {
                options = HiveDatabaseOptions.FromConfiguration(
                    configuration,
                    material);
            }
            catch (ArgumentException)
            {
                return Result.Failure(
                    Error.Validation(
                        "hive.management.persistence-configuration-invalid",
                        "The Hive persistence configuration is invalid."));
            }

            var migration = await new HiveDatabaseMigrator(options)
                .MigrateAsync(cancellationToken)
                .ConfigureAwait(false);

            return migration.IsSuccess
                ? Result.Success()
                : Result.Failure(migration.Error!);
        }
        finally
        {
            material?.Dispose();
        }
    }


}