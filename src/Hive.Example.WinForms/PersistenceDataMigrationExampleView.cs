using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Management;
using Hive.Persistence;
using Microsoft.Data.SqlClient;

namespace Hive.Example.WinForms;

internal sealed class PersistenceDataMigrationExampleView : UserControl
{
    private readonly HiveExampleTestSurface _surface;
    private readonly IHiveExampleOutput _output;

    public PersistenceDataMigrationExampleView(
        IHiveExampleOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        _output = output;

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _surface = new HiveExampleTestSurface
        {
            Dock = DockStyle.Fill,
            RunButtonText = "Run full-data migration"
        };

        _surface.SetInformation(
            "Exercises the public Hive.Management persistence migration boundary with an isolated SQL Server source and Embedded destination.",
            "The example creates representative provider, credential, execution-target, AgentDefinition, favorite, WorkItem, attachment, and event/outbox state; migrates it through the Management facade; then verifies destination state and DPAPI secret re-protection without activating the destination.",
            "Boundary",
            "Hive.Example.WinForms → Hive.Management → Hive.Persistence; the real Settings / Data Migration UI remains a later Slice 5 capability.");

        _surface.CodeSnippet = """
            var result = await management.MigratePersistenceDataAsync(
                new HivePersistenceMigrationRequest(
                    destinationConfiguration),
                accessContext,
                cancellationToken);
            """;

        _surface.ConfigureRun(
            RunExampleAsync,
            _output,
            FindForm());

        Controls.Add(_surface);

        if (FindForm() is HiveForm form)
            form.ThemeManager.Apply(this);
    }

    private async Task RunExampleAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var databaseName =
            $"Hive_Example_Migration_{Guid.NewGuid():N}";
        var embeddedDirectory = Path.Combine(
            Path.GetTempPath(),
            "Hive.Example.WinForms",
            $"Migration_{Guid.NewGuid():N}");
        Directory.CreateDirectory(embeddedDirectory);

        var embeddedPath = Path.Combine(
            embeddedDirectory,
            "hive.db");

        try
        {
            var sourceConfiguration =
                HivePersistenceConfiguration.LocalDevelopment(databaseName);
            var sourceOptions =
                HiveDatabaseOptions.LocalDevelopment(databaseName);

            var migration = await new HiveDatabaseMigrator(
                sourceOptions).MigrateAsync(cancellationToken);

            EnsureSuccess(
                migration,
                "Source database initialization");

            var context = new ResourceAccessContext(
                DeploymentId.New(),
                TenantId.New(),
                PrincipalId.New());

            var configurationStore =
                new ExampleConfigurationStore(sourceConfiguration);
            var quiescence = new ExampleQuiescence();

            using var management = new HiveManagementFacade(
                new SqlProviderResourceStore(sourceOptions),
                new SqlAgentDefinitionResourceStore(sourceOptions),
                new SqlWorkItemResourceStore(sourceOptions),
                secrets: new SqlDpapiSecretStore(sourceOptions),
                configurationStore: configurationStore,
                executionTargetPreferences:
                    new SqlExecutionTargetPreferenceStore(sourceOptions),
                persistenceMigrationQuiescence: quiescence);

            var secretValue = $"example-migration-{Guid.NewGuid():N}";
            SecretId secretId;

            using (var material = SecretMaterial.Create(secretValue))
            {
                var secret = new Secret(
                    new ResourceEnvelope<SecretId>(
                        ResourceKind.Secret,
                        SecretId.New(),
                        context.PrincipalId!.Value,
                        ResourceScope.Tenant(context.TenantId!.Value),
                        ResourceVersion.Initial,
                        new ResourceProvenance(
                            context.PrincipalId.Value,
                            DateTimeOffset.UtcNow,
                            CorrelationId.New()),
                        ResourceLifecycle.Active(
                            DateTimeOffset.UtcNow)),
                    $"example-migration-secret-{Guid.NewGuid():N}",
                    "Migration Example Secret");

                var createdSecret = await management.CreateSecretAsync(
                    secret.Key,
                    secret.DisplayName,
                    material,
                    context,
                    cancellationToken);

                EnsureSuccess(
                    createdSecret,
                    "Example secret creation");

                secretId = createdSecret.Value!.Id;
            }

            var principal = context.PrincipalId.Value;
            var tenant = context.TenantId.Value;
            var now = DateTimeOffset.UtcNow;

            var provider = new Provider(
                new ResourceEnvelope<ProviderId>(
                    ResourceKind.Provider,
                    ProviderId.New(),
                    principal,
                    ResourceScope.Tenant(tenant),
                    ResourceVersion.Initial,
                    new ResourceProvenance(
                        principal,
                        now,
                        CorrelationId.New()),
                    ResourceLifecycle.Active(now)),
                $"example-migration-provider-{Guid.NewGuid():N}",
                "Migration Example Provider",
                "openai-compatible");

            var createdProvider = await management.CreateProviderAsync(
                provider,
                context,
                cancellationToken);
            EnsureSuccess(
                createdProvider,
                "Example Provider creation");

            var account = new ProviderAccount(
                new ResourceEnvelope<ProviderAccountId>(
                    ResourceKind.ProviderAccount,
                    ProviderAccountId.New(),
                    principal,
                    ResourceScope.Tenant(tenant),
                    ResourceVersion.Initial,
                    new ResourceProvenance(
                        principal,
                        now,
                        CorrelationId.New()),
                    ResourceLifecycle.Active(now)),
                provider.Id,
                $"example-migration-account-{Guid.NewGuid():N}",
                "Migration Example Account",
                "example-migration-account",
                new SecretReference(secretId));

            var createdAccount = await management.CreateProviderAccountAsync(
                account,
                context,
                cancellationToken);
            EnsureSuccess(
                createdAccount,
                "Example ProviderAccount creation");

            var target = new ExecutionTarget(
                new ResourceEnvelope<ExecutionTargetId>(
                    ResourceKind.ExecutionTarget,
                    ExecutionTargetId.New(),
                    principal,
                    ResourceScope.Tenant(tenant),
                    ResourceVersion.Initial,
                    new ResourceProvenance(
                        principal,
                        now,
                        CorrelationId.New()),
                    ResourceLifecycle.Active(now)),
                provider.Id,
                account.Id,
                $"example-migration-target-{Guid.NewGuid():N}",
                "Migration Example Target",
                new Uri("https://example.test/v1"),
                "example-migration-model",
                null,
                [
                    new CapabilityStateEntry(
                        HiveCapabilityKeys.TextGeneration,
                        CapabilityState.Supported)
                ]);

            var createdTarget = await management.CreateExecutionTargetAsync(
                target,
                context,
                cancellationToken);
            EnsureSuccess(
                createdTarget,
                "Example ExecutionTarget creation");

            var favorite = await management.ReplaceFavoriteExecutionTargetIdsAsync(
                [target.Id],
                context,
                cancellationToken);
            EnsureSuccess(
                favorite,
                "Example favorite target creation");

            var definition = new AgentDefinition(
                new ResourceEnvelope<AgentDefinitionId>(
                    ResourceKind.AgentDefinition,
                    AgentDefinitionId.New(),
                    principal,
                    ResourceScope.Tenant(tenant),
                    ResourceVersion.Initial,
                    new ResourceProvenance(
                        principal,
                        now,
                        CorrelationId.New()),
                    ResourceLifecycle.Active(now)),
                $"example-migration-agent-{Guid.NewGuid():N}",
                "Migration Example Agent",
                AgentGeneration.Base,
                target.Id);

            var createdDefinition =
                await management.CreateAgentDefinitionAsync(
                    definition,
                    context,
                    cancellationToken);
            EnsureSuccess(
                createdDefinition,
                "Example AgentDefinition creation");

            var workItem = await management.CreateImageWorkItemAsync(
                new WorkItemImageSubmission(
                    "migration-example.png",
                    "image/png",
                    new byte[] { 1, 2, 3, 4, 5 }),
                context,
                cancellationToken);
            EnsureSuccess(
                workItem,
                "Example WorkItem creation");

            var destinationConfiguration =
                HivePersistenceConfiguration.Embedded(embeddedPath);

            var migrationResult =
                await management.MigratePersistenceDataAsync(
                    new HivePersistenceMigrationRequest(
                        destinationConfiguration),
                    context,
                    cancellationToken);

            EnsureSuccess(
                migrationResult,
                "Full-data migration");

            await using var destination = new EmbeddedPersistenceDatabase(
                destinationConfiguration);

            var destinationProviderStore =
                new EmbeddedProviderResourceStore(destination);

            var destinationAccount =
                await destinationProviderStore.GetProviderAccountAsync(
                    account.Id,
                    context,
                    cancellationToken);
            EnsureSuccess(
                destinationAccount,
                "Destination ProviderAccount verification");

            var destinationTarget =
                await destinationProviderStore.GetExecutionTargetAsync(
                    target.Id,
                    context,
                    cancellationToken);
            EnsureSuccess(
                destinationTarget,
                "Destination ExecutionTarget verification");

            var destinationAgentDefinition =
                await new EmbeddedAgentDefinitionResourceStore(destination)
                    .GetAgentDefinitionAsync(
                        definition.Id,
                        context,
                        cancellationToken);
            EnsureSuccess(
                destinationAgentDefinition,
                "Destination AgentDefinition verification");

            var destinationSecretStore =
                new EmbeddedDpapiSecretStore(destination);

            var destinationSecret =
                await destinationSecretStore.GetAsync(
                    secretId,
                    context,
                    cancellationToken);
            EnsureSuccess(
                destinationSecret,
                "Destination secret verification");

            bool secretRoundTrip;
            using (destinationSecret.Value!.Material)
            {
                secretRoundTrip = string.Equals(
                    destinationSecret.Value.Material.Reveal(),
                    secretValue,
                    StringComparison.Ordinal);
            }

            if (!secretRoundTrip)
            {
                throw new InvalidOperationException(
                    "Destination secret material did not round-trip correctly.");
            }

            var destinationFavorites =
                new EmbeddedExecutionTargetPreferenceStore(destination);

            var destinationFavoriteIds =
                await destinationFavorites.GetFavoriteExecutionTargetIdsAsync(
                    context,
                    cancellationToken);
            EnsureSuccess(
                destinationFavoriteIds,
                "Destination favorite verification");

            var destinationWorkItem =
                await new EmbeddedWorkItemResourceStore(destination)
                    .GetWorkItemAsync(
                        workItem.Value!.Id,
                        context,
                        cancellationToken);
            EnsureSuccess(
                destinationWorkItem,
                "Destination WorkItem verification");

            var destinationAttachment =
                await new EmbeddedWorkItemResourceStore(destination)
                    .GetWorkItemAttachmentAsync(
                        workItem.Value.Id,
                        context,
                        cancellationToken);
            EnsureSuccess(
                destinationAttachment,
                "Destination WorkItem attachment verification");

            var destinationManagementMode =
                destinationTarget.Value!.ManagementMode;

            if (destinationManagementMode !=
                target.ManagementMode)
            {
                throw new InvalidOperationException(
                    "Destination ExecutionTarget management mode did not round-trip.");
            }

            _output.Write(
                "SQL Server ↔ Embedded Full-Data Migration",
                $"""
                Source database: {databaseName}
                Migration: {migrationResult.Value!.MigrationId}
                Schema: {migrationResult.Value.SourceSchemaVersion} → {migrationResult.Value.DestinationSchemaVersion}
                Records migrated: {migrationResult.Value.TotalRecordsMigrated}
                Source verified unchanged: {migrationResult.Value.SourceVerifiedUnchanged}
                Destination verified: {migrationResult.Value.DestinationVerified}
                Destination activated: {migrationResult.Value.DestinationActivated}
                Quiescence acquired/released: {quiescence.Acquired}/{quiescence.Released}
                ProviderAccount credential reference preserved: {destinationAccount.Value!.CredentialSecret?.Id == secretId}
                ExecutionTarget preserved: {destinationTarget.Value!.Id}
                AgentDefinition preserved: {destinationAgentDefinition.Value!.Id}
                Secret re-protected and readable: {secretRoundTrip} (value not printed)
                Favorites preserved: {string.Join(", ", destinationFavoriteIds.Value!)}
                WorkItem preserved: {destinationWorkItem.Value!.Id}
                Attachment bytes preserved: {destinationAttachment.Value!.Content.Length}
                Migrated record counts: {string.Join(", ", migrationResult.Value.RecordCounts.OrderBy(static item => item.Key).Select(static item => $"{item.Key}={item.Value}"))}
                """);
        finally
        {
            await DropSqlDatabaseAsync(databaseName);
            TryDeleteDirectory(embeddedDirectory);
        }
    }

    private static async Task DropSqlDatabaseAsync(
        string databaseName)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = HiveDatabaseOptions.LocalDevelopment().ServerName,
                InitialCatalog = "master",
                IntegratedSecurity = true,
                TrustServerCertificate = true,
                ConnectRetryCount = 0,
                ApplicationName = "Hive.Example.WinForms"
            };

            await using var connection = new SqlConnection(
                builder.ConnectionString);
            await connection.OpenAsync();

            var quoted =
                $"[{databaseName.Replace("]", "]]", StringComparison.Ordinal)}]";

            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                IF DB_ID(@DatabaseName) IS NOT NULL
                BEGIN
                    ALTER DATABASE {quoted} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE {quoted};
                END;
                """;
            command.Parameters.Add(
                new SqlParameter(
                    "@DatabaseName",
                    databaseName));

            await command.ExecuteNonQueryAsync();
        }
        catch (SqlException)
        {
        }
    }

    private static void TryDeleteDirectory(
        string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch
        {
        }
    }

    private static void EnsureSuccess<T>(
        Result<T> result,
        string operation)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"{operation} failed: {result.Error?.Code} [{result.Error?.Category}] {result.Error?.Message}");
        }
    }

    private static readonly string[] MigrationTables =
    [
        "HiveProviders",
        "HiveProviderAccounts",
        "HiveExecutionTargets",
        "HiveSecrets",
        "HiveEventLog",
        "HiveEventSnapshots",
        "HiveEventOutbox",
        "HiveAgentDefinitions",
        "HiveWorkItems",
        "HiveWorkItemAttachments",
        "HiveExecutionTargetFavorites",
        "HiveAgentWorkState"
    ];

    private sealed class ExampleConfigurationStore :
        IHiveConfigurationStore
    {
        private readonly HivePersistenceConfiguration _configuration;

        public ExampleConfigurationStore(
            HivePersistenceConfiguration configuration) =>
            _configuration = configuration;

        public Task<Result<HivePersistenceConfiguration>>
            LoadPersistenceConfigurationAsync(
                CancellationToken cancellationToken = default) =>
            Task.FromResult(
                Result<HivePersistenceConfiguration>.Success(
                    _configuration));

        public Task<Result<HivePersistenceConfiguration>>
            SavePersistenceConfigurationAsync(
                HivePersistenceConfiguration configuration,
                CancellationToken cancellationToken = default) =>
            Task.FromResult(
                Result<HivePersistenceConfiguration>.Success(
                    configuration));
    }

    private sealed class ExampleQuiescence :
        IHivePersistenceMigrationQuiescence
    {
        public bool Acquired { get; private set; }
        public bool Released { get; private set; }

        public Task<Result<IAsyncDisposable>> AcquireAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Acquired = true;

            return Task.FromResult(
                Result<IAsyncDisposable>.Success(
                    new Lease(() => Released = true)));
        }

        private sealed class Lease :
            IAsyncDisposable
        {
            private readonly Action _release;

            public Lease(Action release) =>
                _release = release;

            public ValueTask DisposeAsync()
            {
                _release();
                return ValueTask.CompletedTask;
            }
        }
    }
}
