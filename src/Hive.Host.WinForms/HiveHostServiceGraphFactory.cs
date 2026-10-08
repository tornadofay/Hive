using Hive.Coordination;
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

public sealed class HiveHostServiceGraphFactory :
    IHiveHostServiceGraphFactory
{
    private static readonly HttpClient SharedHttpClient = new();

    private readonly IHiveBootstrapCredentialStore _bootstrapCredentials;
    private readonly IHiveConfigurationStore _configurationStore;

    public HiveHostServiceGraphFactory(
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

        return await (configuration.Backend switch
        {
            HivePersistenceBackend.SqlServer =>
                CreateSqlServerAsync(configuration, cancellationToken),
            HivePersistenceBackend.Embedded =>
                CreateEmbeddedAsync(configuration, cancellationToken),
            _ =>
                Task.FromResult(
                    Result<HiveHostServiceGraph>.Failure(
                        Error.Validation(
                            "hive.host.persistence-backend-invalid",
                            "The persistence backend is invalid.")))
        }).ConfigureAwait(false);
    }

    private async Task<Result<HiveHostServiceGraph>> CreateEmbeddedAsync(
        HivePersistenceConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        EmbeddedPersistenceDatabase? database = null;
        HiveManagementFacade? management = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            database = new EmbeddedPersistenceDatabase(configuration);

            var secretStore = new EmbeddedDpapiSecretStore(database);
            var eventPersistence = HiveEventPersistence.CreateEmbedded(database);
            var agentExecution = new AgentExecutionService(
                eventPersistence,
                SharedHttpClient);
            var structuredExtractionEngine =
                new StructuredExtractionEngine(SharedHttpClient);
            var structuredExtractionBatches =
                new SqlStructuredExtractionBatchStore(eventPersistence);

            management = new HiveManagementFacade(
                new EmbeddedProviderResourceStore(database),
                new EmbeddedAgentDefinitionResourceStore(database),
                new EmbeddedWorkItemResourceStore(database),
                secretStore,
                new OpenAICompatibleProviderConnectionTester(),
                _configurationStore,
                new HivePersistenceConnectionTester(),
                _bootstrapCredentials,
                agentExecution,
                new OpenAICompatibleProviderCapabilityDiscovery(
                    SharedHttpClient),
                executionTargetPreferences:
                    new EmbeddedExecutionTargetPreferenceStore(database),
                structuredExtractionBatches: structuredExtractionBatches,
                structuredExtractionEngine: structuredExtractionEngine);

            var graph = new HiveHostServiceGraph(
                configuration,
                management,
                [database, management]);

            database = null;
            management = null;

            return Result<HiveHostServiceGraph>.Success(graph);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ArgumentException)
        {
            return Result<HiveHostServiceGraph>.Failure(
                Error.Validation(
                    "hive.host.persistence-configuration-invalid",
                    "The persistence configuration is invalid."));
        }
        catch (Exception)
        {
            return Result<HiveHostServiceGraph>.Failure(
                new Error(
                    "hive.host.service-graph-construction-failed",
                    ErrorCategory.External,
                    "Hive service graph construction failed."));
        }
        finally
        {
            management?.Dispose();
            database?.Dispose();
        }
    }

    private async Task<Result<HiveHostServiceGraph>> CreateSqlServerAsync(
        HivePersistenceConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        SecretMaterial? credential = null;
        HiveManagementFacade? management = null;

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
                    var error = resolved.Error!;

                    return Result<HiveHostServiceGraph>.Failure(
                        new Error(
                            error.Code,
                            error.Category,
                            "The Hive bootstrap credential could not be resolved."));
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
            catch (ArgumentException)
            {
                return Result<HiveHostServiceGraph>.Failure(
                    Error.Validation(
                        "hive.host.persistence-configuration-invalid",
                        "The persistence configuration is invalid."));
            }

            var secretStore = new SqlDpapiSecretStore(options);
            var eventPersistence = HiveEventPersistence.CreateSql(options);
            var agentExecution = new AgentExecutionService(
                eventPersistence,
                SharedHttpClient);
            var structuredExtractionEngine =
                new StructuredExtractionEngine(SharedHttpClient);
            var structuredExtractionBatches =
                new SqlStructuredExtractionBatchStore(eventPersistence);

            management = new HiveManagementFacade(
                new SqlProviderResourceStore(options),
                new SqlAgentDefinitionResourceStore(options),
                new SqlWorkItemResourceStore(options),
                secretStore,
                new OpenAICompatibleProviderConnectionTester(),
                _configurationStore,
                new HivePersistenceConnectionTester(),
                _bootstrapCredentials,
                agentExecution,
                new OpenAICompatibleProviderCapabilityDiscovery(
                    SharedHttpClient),
                executionTargetPreferences: new SqlExecutionTargetPreferenceStore(options),
                structuredExtractionBatches: structuredExtractionBatches,
                structuredExtractionEngine: structuredExtractionEngine);

            var graph = new HiveHostServiceGraph(
                configuration,
                management,
                [management]);

            management = null;

            return Result<HiveHostServiceGraph>.Success(graph);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Result<HiveHostServiceGraph>.Failure(
                new Error(
                    "hive.host.service-graph-construction-failed",
                    ErrorCategory.External,
                    "Hive service graph construction failed."));
        }
        finally
        {
            management?.Dispose();
            credential?.Dispose();
        }
    }
}
