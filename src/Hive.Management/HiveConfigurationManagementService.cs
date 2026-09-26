using Hive.Core;

using Hive.Persistence;



namespace Hive.Management;



internal sealed class HiveConfigurationManagementService : HiveManagementServiceBase, IDisposable

{

    private readonly IHiveConfigurationStore? _configurationStore;

    private readonly IHivePersistenceConnectionTester? _persistenceConnectionTester;

    private readonly IHiveBootstrapCredentialStore? _bootstrapCredentials;

    private readonly SemaphoreSlim _bootstrapConfigurationMutationGate;
    private readonly object _mutationLifetimeGate = new();
    private int _activeMutationOperations;
    private int _disposed;
    private bool _mutationGateDisposed;



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

        using var mutation = await EnterMutationAsync(cancellationToken).ConfigureAwait(false);
        if (mutation is null)
        {
            return Result<HivePersistenceConfiguration>.Failure(
                Error.Unsupported(
                    "hive.management.disposed",
                    "The Hive management configuration service has been disposed."));
        }

        return await _configurationStore
            .SavePersistenceConfigurationAsync(
                configuration,
                cancellationToken)
            .ConfigureAwait(false);
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

        using var mutation = await EnterMutationAsync(cancellationToken).ConfigureAwait(false);
        if (mutation is null)
        {
            return Result<HiveBootstrapCredentialReference>.Failure(
                Error.Unsupported(
                    "hive.management.disposed",
                    "The Hive management configuration service has been disposed."));
        }

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

        using var mutation = await EnterMutationAsync(cancellationToken).ConfigureAwait(false);
        if (mutation is null)
        {
            return Result.Failure(
                Error.Unsupported(
                    "hive.management.disposed",
                    "The Hive management configuration service has been disposed."));
        }

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



    public void Dispose()
    {
        lock (_mutationLifetimeGate)
        {
            if (_disposed != 0)
                return;

            _disposed = 1;

            if (_activeMutationOperations == 0 &&
                !_mutationGateDisposed)
            {
                _bootstrapConfigurationMutationGate.Dispose();
                _mutationGateDisposed = true;
            }
        }

        GC.SuppressFinalize(this);
    }

    private async Task<MutationLease?> EnterMutationAsync(
        CancellationToken cancellationToken)
    {
        lock (_mutationLifetimeGate)
        {
            if (_disposed != 0)
                return null;

            _activeMutationOperations++;
        }

        try
        {
            await _bootstrapConfigurationMutationGate
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            return new MutationLease(this);
        }
        catch
        {
            ExitMutationOperation();
            throw;
        }
    }

    private void ExitMutationOperation()
    {
        lock (_mutationLifetimeGate)
        {
            _activeMutationOperations--;

            if (_disposed != 0 &&
                _activeMutationOperations == 0 &&
                !_mutationGateDisposed)
            {
                _bootstrapConfigurationMutationGate.Dispose();
                _mutationGateDisposed = true;
            }
        }
    }

    private sealed class MutationLease : IDisposable
    {
        private HiveConfigurationManagementService? _owner;

        public MutationLease(HiveConfigurationManagementService owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            if (owner is null)
                return;

            owner._bootstrapConfigurationMutationGate.Release();
            owner.ExitMutationOperation();
        }
    }

}