using Hive.Core;
using Hive.Management;
using Hive.Persistence;
using Hive.Providers.OpenAICompatible;

namespace Hive.Host.WinForms;

public interface IHiveHostServiceGraphFactory
{
    Task<Result<HiveHostServiceGraph>> CreateAsync(
        HivePersistenceConfiguration configuration,
        CancellationToken cancellationToken = default);
}

public sealed class SqlHiveHostServiceGraphFactory :
    IHiveHostServiceGraphFactory
{
    private readonly IHiveBootstrapCredentialStore _bootstrapCredentials;
    private readonly IHiveConfigurationStore _configurationStore;

    public SqlHiveHostServiceGraphFactory(
        IHiveBootstrapCredentialStore bootstrapCredentials,
        IHiveConfigurationStore configurationStore)
    {
        _bootstrapCredentials = bootstrapCredentials
            ?? throw new ArgumentNullException(nameof(bootstrapCredentials));
        _configurationStore = configurationStore
            ?? throw new ArgumentNullException(nameof(configurationStore));
    }

    public async Task<Result<HiveHostServiceGraph>> CreateAsync(
        HivePersistenceConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        SecretMaterial? credential = null;

        try
        {
            if (configuration.AuthenticationMode ==
                HiveSqlAuthenticationMode.SqlPassword)
            {
                if (configuration.BootstrapCredential is null)
                {
                    return Result<HiveHostServiceGraph>.Failure(
                        Error.Validation(
                            "hive.host.bootstrap-credential-reference-required",
                            "SQL password persistence configuration requires a bootstrap credential reference."));
                }

                var resolved = await _bootstrapCredentials
                    .ResolveAsync(
                        configuration.BootstrapCredential.Value,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (resolved.IsFailure)
                {
                    return Result<HiveHostServiceGraph>.Failure(
                        resolved.Error!);
                }

                credential = resolved.Value;

                if (credential is null)
                {
                    return Result<HiveHostServiceGraph>.Failure(
                        new Error(
                            "hive.host.bootstrap-credential-empty",
                            ErrorCategory.Internal,
                            "The bootstrap credential boundary returned no credential material."));
                }
            }

            HiveDatabaseOptions options;

            try
            {
                options = HiveDatabaseOptions.FromConfiguration(
                    configuration,
                    credential);
            }
            catch (ArgumentException exception)
            {
                return Result<HiveHostServiceGraph>.Failure(
                    Error.Validation(
                        "hive.host.persistence-configuration-invalid",
                        exception.Message));
            }

            var management = new HiveManagementFacade(
                new SqlProviderResourceStore(options),
                new SqlAgentDefinitionResourceStore(options),
                new SqlWorkItemResourceStore(options),
                new SqlDpapiSecretStore(options),
                new OpenAICompatibleProviderConnectionTester(),
                _configurationStore,
                new HivePersistenceConnectionTester(),
                _bootstrapCredentials);

            return Result<HiveHostServiceGraph>.Success(
                new HiveHostServiceGraph(
                    configuration,
                    management));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result<HiveHostServiceGraph>.Failure(
                new Error(
                    "hive.host.service-graph-construction-failed",
                    ErrorCategory.External,
                    $"Hive service graph construction failed: {exception.Message}"));
        }
        finally
        {
            credential?.Dispose();
        }
    }
}
