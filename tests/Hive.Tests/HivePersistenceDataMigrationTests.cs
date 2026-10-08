using System.Text.Json;
using Hive.Agents;
using Hive.Core;
using Hive.Management;
using Hive.Persistence;
using Hive.Tests.TestInfrastructure;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Hive.Tests;

public sealed class HivePersistenceDataMigrationTests
{
    [Fact]
    public async Task FullDataMigration_RoundTripsAllCurrentDurableStateAndReprotectsSecrets()
    {
        var sourceDatabaseName = $"Hive_Test_Migration_Source_{Guid.NewGuid():N}";
        var roundTripDatabaseName = $"Hive_Test_Migration_RoundTrip_{Guid.NewGuid():N}";
        var embeddedDirectory = CreateEmbeddedDirectory("round-trip");
        var embeddedPath = Path.Combine(embeddedDirectory, "hive.db");

        try
        {
            var sourceOptions = await CreateSqlDatabaseAsync(sourceDatabaseName);
            var sourceConfiguration = CreateSqlConfiguration(sourceDatabaseName);

            var seed = await SeedRepresentativeSqlDataAsync(
                sourceOptions);

            var embeddedConfiguration = HivePersistenceConfiguration.Embedded(
                embeddedPath);

            var migrator = new HivePersistenceDataMigrator();
            var migrationId = Guid.NewGuid();

            var first = await migrator.MigrateAsync(
                sourceConfiguration,
                null,
                embeddedConfiguration,
                null,
                migrationId,
                seed.Context);

            Assert.True(first.IsSuccess, first.Error?.Message);
            Assert.Equal(migrationId, first.Value!.MigrationId);
            Assert.Equal(
                HivePersistenceBackend.SqlServer,
                first.Value.SourceBackend);
            Assert.Equal(
                HivePersistenceBackend.Embedded,
                first.Value.DestinationBackend);
            Assert.Equal(
                HiveDatabaseSchema.CurrentSchemaVersion,
                first.Value.SourceSchemaVersion);
            Assert.Equal(
                EmbeddedPersistenceSchema.CurrentSchemaVersion,
                first.Value.DestinationSchemaVersion);
            Assert.True(first.Value.SourceVerifiedUnchanged);
            Assert.True(first.Value.DestinationVerified);

            await using var embedded = new EmbeddedPersistenceDatabase(
                embeddedConfiguration);

            var sourceCountsBefore = await ReadSqlCountsAsync(
                sourceOptions);
            var embeddedCounts = await ReadEmbeddedCountsAsync(
                embedded);

            AssertCountsEqual(sourceCountsBefore, embeddedCounts);
            AssertCountsEqual(sourceCountsBefore, first.Value.RecordCounts);

            var expectedTotal = sourceCountsBefore.Values.Sum();
            Assert.Equal(
                expectedTotal,
                first.Value.TotalRecordsMigrated);

            var embeddedProviderStore = new EmbeddedProviderResourceStore(embedded);
            var embeddedAccount = await embeddedProviderStore.GetProviderAccountAsync(
                seed.ProviderAccountId,
                seed.Context);
            Assert.True(
                embeddedAccount.IsSuccess,
                embeddedAccount.Error?.Message);
            Assert.Equal(
                seed.SecretId,
                embeddedAccount.Value!.CredentialSecret!.Value.Id);

            var embeddedSecretStore = new EmbeddedDpapiSecretStore(embedded);
            var embeddedSecret = await embeddedSecretStore.GetAsync(
                seed.SecretId,
                seed.Context);
            Assert.True(
                embeddedSecret.IsSuccess,
                embeddedSecret.Error?.Message);
            using (embeddedSecret.Value!.Material)
            {
                Assert.Equal(
                    seed.SecretValue,
                    embeddedSecret.Value.Material.Reveal());
            }

            var embeddedFavorites =
                new EmbeddedExecutionTargetPreferenceStore(embedded);
            var favoriteIds = await embeddedFavorites.GetFavoriteExecutionTargetIdsAsync(
                seed.Context);
            Assert.True(favoriteIds.IsSuccess, favoriteIds.Error?.Message);
            Assert.Equal(seed.FavoriteTargetIds, favoriteIds.Value);

            var embeddedWorkItems = new EmbeddedWorkItemResourceStore(embedded);
            var embeddedWorkItem = await embeddedWorkItems.GetWorkItemAsync(
                seed.WorkItemId,
                seed.Context);
            Assert.True(
                embeddedWorkItem.IsSuccess,
                embeddedWorkItem.Error?.Message);
            Assert.Equal(
                seed.WorkItemId,
                embeddedWorkItem.Value!.Id);

            var embeddedAttachment = await embeddedWorkItems.GetWorkItemAttachmentAsync(
                seed.WorkItemId,
                seed.Context);
            Assert.True(
                embeddedAttachment.IsSuccess,
                embeddedAttachment.Error?.Message);
            Assert.Equal(
                seed.AttachmentContent,
                embeddedAttachment.Value!.Content.ToArray());

            var embeddedEvents = HiveEventPersistence.CreateEmbedded(
                embedded).EventStore;
            var events = await embeddedEvents.ReadEventsAsync(seed.EventStream);
            Assert.True(events.IsSuccess, events.Error?.Message);
            Assert.Equal(2, events.Value!.Count);
            Assert.Equal(1, events.Value[0].StreamVersion.Value);
            Assert.Equal("work-item.created", events.Value[0].Envelope.EventType.Value);
            Assert.Equal(2, events.Value[1].StreamVersion.Value);
            Assert.Equal(seed.EventId, events.Value[1].Envelope.EventId);

            var snapshot = await embeddedEvents.GetSnapshotAsync(seed.EventStream);
            Assert.True(snapshot.IsSuccess, snapshot.Error?.Message);
            Assert.Equal(
                seed.EventSnapshotVersion,
                snapshot.Value!.Version);

            var outbox = await embeddedEvents.GetOutboxAsync(seed.EventId);
            Assert.True(outbox.IsSuccess, outbox.Error?.Message);
            Assert.NotNull(outbox.Value);

            var embeddedAgentWork =
                HiveAgentWorkPersistence.CreateEmbedded(embedded);
            var recoveredRuntime = seed.Agent.CreateRuntimeInstance(
                seed.Now.AddMinutes(1),
                clock: new FixedClock(seed.Now.AddMinutes(1)),
                workStores: embeddedAgentWork,
                runtimeId: seed.RuntimeId);
            var recoveredContext = new ResourceAccessContext(
                seed.Context.DeploymentId,
                seed.Context.TenantId,
                seed.Context.PrincipalId,
                AgentId: seed.Agent.Id,
                RuntimeId: seed.RuntimeId);

            var recoveredObjective = recoveredRuntime.Work.Objectives.Get(
                recoveredContext,
                seed.Agent.Id,
                seed.RuntimeId,
                seed.ObjectiveId);
            Assert.True(
                recoveredObjective.IsSuccess,
                recoveredObjective.Error?.Message);
            Assert.Equal(seed.ObjectiveId, recoveredObjective.Value!.Id);

            var recoveredMemory = recoveredRuntime.Work.Memory.Get(
                recoveredContext,
                seed.Agent.Id,
                seed.RuntimeId,
                seed.MemoryId);
            Assert.True(
                recoveredMemory.IsSuccess,
                recoveredMemory.Error?.Message);
            Assert.Equal(
                seed.MemoryContent,
                recoveredMemory.Value!.Content);

            var sourceSecretStore = new SqlDpapiSecretStore(sourceOptions);
            var sourceSecretAfter = await sourceSecretStore.GetAsync(
                seed.SecretId,
                seed.Context);
            Assert.True(
                sourceSecretAfter.IsSuccess,
                sourceSecretAfter.Error?.Message);
            using (sourceSecretAfter.Value!.Material)
            {
                Assert.Equal(
                    seed.SecretValue,
                    sourceSecretAfter.Value.Material.Reveal());
            }

            var second = await migrator.MigrateAsync(
                embeddedConfiguration,
                null,
                CreateSqlConfiguration(roundTripDatabaseName),
                null,
                Guid.NewGuid(),
                seed.Context);

            Assert.True(second.IsSuccess, second.Error?.Message);
            Assert.Equal(
                HivePersistenceBackend.Embedded,
                second.Value!.SourceBackend);
            Assert.Equal(
                HivePersistenceBackend.SqlServer,
                second.Value.DestinationBackend);
            Assert.True(second.Value.SourceVerifiedUnchanged);
            Assert.True(second.Value.DestinationVerified);

            var roundTripOptions = new HiveDatabaseOptions(
                new SqlConnectionStringBuilder(
                    HivePersistenceTestConfiguration.ConnectionString)
                {
                    InitialCatalog = roundTripDatabaseName,
                    ApplicationName = "Hive.Tests"
                }.ConnectionString,
                createDatabaseIfMissing: true);

            var roundTripCounts = await ReadSqlCountsAsync(
                roundTripOptions);
            AssertCountsEqual(sourceCountsBefore, roundTripCounts);

            var roundTripSecretStore = new SqlDpapiSecretStore(
                roundTripOptions);
            var roundTripSecret = await roundTripSecretStore.GetAsync(
                seed.SecretId,
                seed.Context);
            Assert.True(
                roundTripSecret.IsSuccess,
                roundTripSecret.Error?.Message);
            using (roundTripSecret.Value!.Material)
            {
                Assert.Equal(
                    seed.SecretValue,
                    roundTripSecret.Value.Material.Reveal());
            }
        }
        finally
        {
            await DropSqlDatabaseAsync(sourceDatabaseName);
            await DropSqlDatabaseAsync(roundTripDatabaseName);
            TryDeleteDirectory(embeddedDirectory);
        }
    }

    [Fact]
    public async Task ManagementMigration_RequiresQuiescenceAndLeavesDestinationInactive()
    {
        var sourceDatabaseName = $"Hive_Test_Migration_Management_{Guid.NewGuid():N}";
        var embeddedDirectory = CreateEmbeddedDirectory("management");
        var embeddedPath = Path.Combine(embeddedDirectory, "hive.db");

        try
        {
            var sourceOptions = await CreateSqlDatabaseAsync(sourceDatabaseName);
            var sourceConfiguration = CreateSqlConfiguration(sourceDatabaseName);
            var configurationStore = new TestConfigurationStore(
                sourceConfiguration);
            var context = new ResourceAccessContext(
                DeploymentId.New(),
                TenantId.New(),
                PrincipalId.New());

            using (var unavailable = new HiveManagementFacade(
                       new SqlProviderResourceStore(sourceOptions),
                       new SqlAgentDefinitionResourceStore(sourceOptions),
                       new SqlWorkItemResourceStore(sourceOptions),
                       configurationStore: configurationStore))
            {
                var unavailableResult = await unavailable.MigratePersistenceDataAsync(
                    new HivePersistenceMigrationRequest(
                        HivePersistenceConfiguration.Embedded(embeddedPath)),
                    context);

                Assert.False(unavailableResult.IsSuccess);
                Assert.Equal(
                    "hive.management.persistence-migration-quiescence-unavailable",
                    unavailableResult.Error!.Code);
            }

            var quiescence = new RecordingQuiescence();
            using var management = new HiveManagementFacade(
                new SqlProviderResourceStore(sourceOptions),
                new SqlAgentDefinitionResourceStore(sourceOptions),
                new SqlWorkItemResourceStore(sourceOptions),
                configurationStore: configurationStore,
                persistenceMigrationQuiescence: quiescence);

            var result = await management.MigratePersistenceDataAsync(
                new HivePersistenceMigrationRequest(
                    HivePersistenceConfiguration.Embedded(embeddedPath)),
                context);

            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(
                HivePersistenceMigrationDirection.SqlServerToEmbedded,
                result.Value!.Direction);
            Assert.True(result.Value.DestinationVerified);
            Assert.False(result.Value.DestinationActivated);
            Assert.True(quiescence.Acquired);
            Assert.True(quiescence.Released);
        }
        finally
        {
            await DropSqlDatabaseAsync(sourceDatabaseName);
            TryDeleteDirectory(embeddedDirectory);
        }
    }

    [Fact]
    public async Task ManagementMigration_RejectsUnquiescentSource()
    {
        var sourceDatabaseName = $"Hive_Test_Migration_Busy_{Guid.NewGuid():N}";
        var embeddedDirectory = CreateEmbeddedDirectory("busy");
        var embeddedPath = Path.Combine(embeddedDirectory, "hive.db");

        try
        {
            var sourceOptions = await CreateSqlDatabaseAsync(sourceDatabaseName);
            var sourceConfiguration = CreateSqlConfiguration(sourceDatabaseName);
            var configurationStore = new TestConfigurationStore(
                sourceConfiguration);
            var quiescence = new RecordingQuiescence
            {
                Failure = Error.Conflict(
                    "hive.persistence.data-migration.source-not-quiescent",
                    "The source persistence graph is not quiescent.")
            };

            using var management = new HiveManagementFacade(
                new SqlProviderResourceStore(sourceOptions),
                new SqlAgentDefinitionResourceStore(sourceOptions),
                new SqlWorkItemResourceStore(sourceOptions),
                configurationStore: configurationStore,
                persistenceMigrationQuiescence: quiescence);

            var result = await management.MigratePersistenceDataAsync(
                new HivePersistenceMigrationRequest(
                    HivePersistenceConfiguration.Embedded(embeddedPath)),
                new ResourceAccessContext(
                    DeploymentId.New(),
                    TenantId.New(),
                    PrincipalId.New()));

            Assert.False(result.IsSuccess);
            Assert.Equal(
                "hive.persistence.data-migration.source-not-quiescent",
                result.Error!.Code);
            Assert.False(quiescence.Released);
        }
        finally
        {
            await DropSqlDatabaseAsync(sourceDatabaseName);
            TryDeleteDirectory(embeddedDirectory);
        }
    }

    [Fact]
    public async Task Migration_RejectsNonEmptyEmbeddedDestination()
    {
        var sourceDatabaseName = $"Hive_Test_Migration_NonEmptySource_{Guid.NewGuid():N}";
        var embeddedDirectory = CreateEmbeddedDirectory("non-empty-embedded");
        var embeddedPath = Path.Combine(embeddedDirectory, "hive.db");

        try
        {
            var sourceOptions = await CreateSqlDatabaseAsync(sourceDatabaseName);
            var sourceConfiguration = CreateSqlConfiguration(sourceDatabaseName);

            await using var destination = new EmbeddedPersistenceDatabase(
                HivePersistenceConfiguration.Embedded(embeddedPath));
            var initialization = await destination.InitializeAsync();
            Assert.True(initialization.IsSuccess, initialization.Error?.Message);

            var context = NewContext();
            var provider = CreateProvider(context, "non-empty");
            var store = new EmbeddedProviderResourceStore(destination);
            var created = await store.CreateProviderAsync(provider, context);
            Assert.True(created.IsSuccess, created.Error?.Message);

            var result = await new HivePersistenceDataMigrator().MigrateAsync(
                sourceConfiguration,
                null,
                HivePersistenceConfiguration.Embedded(embeddedPath),
                null,
                Guid.NewGuid(),
                context);

            Assert.False(result.IsSuccess);
            Assert.Equal(
                "hive.persistence.data-migration.destination-not-empty",
                result.Error!.Code);

            var count = await CountEmbeddedRowsAsync(
                destination,
                "HiveProviders");
            Assert.Equal(1, count);
        }
        finally
        {
            await DropSqlDatabaseAsync(sourceDatabaseName);
            TryDeleteDirectory(embeddedDirectory);
        }
    }

    [Fact]
    public async Task Migration_RejectsNonEmptySqlDestination()
    {
        var sourceDatabaseName = $"Hive_Test_Migration_EmbeddedSource_{Guid.NewGuid():N}";
        var destinationDatabaseName = $"Hive_Test_Migration_NonEmptySql_{Guid.NewGuid():N}";
        var sourceDirectory = CreateEmbeddedDirectory("non-empty-sql-source");
        var sourcePath = Path.Combine(sourceDirectory, "hive.db");

        try
        {
            var sourceDatabase = new EmbeddedPersistenceDatabase(
                HivePersistenceConfiguration.Embedded(sourcePath));
            await using (sourceDatabase)
            {
                var initialization = await sourceDatabase.InitializeAsync();
                Assert.True(
                    initialization.IsSuccess,
                    initialization.Error?.Message);
            }

            var destinationOptions = await CreateSqlDatabaseAsync(
                destinationDatabaseName);

            var context = NewContext();
            var provider = CreateProvider(context, "non-empty-sql");
            var store = new SqlProviderResourceStore(destinationOptions);
            var created = await store.CreateProviderAsync(provider, context);
            Assert.True(created.IsSuccess, created.Error?.Message);

            var result = await new HivePersistenceDataMigrator().MigrateAsync(
                HivePersistenceConfiguration.Embedded(sourcePath),
                null,
                CreateSqlConfiguration(destinationDatabaseName),
                null,
                Guid.NewGuid(),
                context);

            Assert.False(result.IsSuccess);
            Assert.Equal(
                "hive.persistence.data-migration.destination-not-empty",
                result.Error!.Code);

            var count = await CountSqlRowsAsync(
                destinationOptions,
                "HiveProviders");
            Assert.Equal(1, count);
        }
        finally
        {
            await DropSqlDatabaseAsync(sourceDatabaseName);
            await DropSqlDatabaseAsync(destinationDatabaseName);
            TryDeleteDirectory(sourceDirectory);
        }
    }

    [Fact]
    public async Task Migration_RejectsFutureEmbeddedDestinationSchema()
    {
        var sourceDatabaseName = $"Hive_Test_Migration_FutureSource_{Guid.NewGuid():N}";
        var destinationDirectory = CreateEmbeddedDirectory("future-destination");
        var destinationPath = Path.Combine(destinationDirectory, "hive.db");

        try
        {
            _ = await CreateSqlDatabaseAsync(sourceDatabaseName);
            var context = NewContext();

            await using var destination = new EmbeddedPersistenceDatabase(
                HivePersistenceConfiguration.Embedded(destinationPath));
            var initialization = await destination.InitializeAsync();
            Assert.True(initialization.IsSuccess, initialization.Error?.Message);

            await using (var connection = await destination.OpenConnectionAsync())
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    UPDATE [HiveSchemaVersion]
                    SET [SchemaVersion] = 16
                    WHERE [SchemaRowId] = 1;
                    """;
                await command.ExecuteNonQueryAsync();
            }

            var result = await new HivePersistenceDataMigrator().MigrateAsync(
                CreateSqlConfiguration(sourceDatabaseName),
                null,
                HivePersistenceConfiguration.Embedded(destinationPath),
                null,
                Guid.NewGuid(),
                context);

            Assert.False(result.IsSuccess);
            Assert.Equal(
                "hive.persistence.embedded.future-schema",
                result.Error!.Code);
        }
        finally
        {
            await DropSqlDatabaseAsync(sourceDatabaseName);
            TryDeleteDirectory(destinationDirectory);
        }
    }

    [Fact]
    public async Task Migration_RejectsPartialSqlDestinationMetadata()
    {
        var sourceDirectory = CreateEmbeddedDirectory("partial-sql-source");
        var sourcePath = Path.Combine(sourceDirectory, "hive.db");
        var destinationDatabaseName = $"Hive_Test_Migration_PartialDestination_{Guid.NewGuid():N}";

        try
        {
            await using (var source = new EmbeddedPersistenceDatabase(
                             HivePersistenceConfiguration.Embedded(sourcePath)))
            {
                var initialization = await source.InitializeAsync();
                Assert.True(initialization.IsSuccess, initialization.Error?.Message);
            }

            var destinationOptions = await CreateSqlDatabaseAsync(destinationDatabaseName);
            var database = new PersistenceTestDatabase(destinationDatabaseName);
            database.Reset();

            await using (var connection = new SqlConnection(
                             destinationOptions.ConnectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE [dbo].[HiveSchemaVersion]
                    (
                        [SchemaRowId] TINYINT NOT NULL PRIMARY KEY,
                        [SchemaVersion] INT NOT NULL,
                        [RecordedAtUtc] DATETIME2(7) NOT NULL
                    );

                    INSERT INTO [dbo].[HiveSchemaVersion]
                    (
                        [SchemaRowId],
                        [SchemaVersion],
                        [RecordedAtUtc]
                    )
                    VALUES
                    (1, 15, SYSUTCDATETIME());
                    """;
                await command.ExecuteNonQueryAsync();
            }

            var result = await new HivePersistenceDataMigrator().MigrateAsync(
                HivePersistenceConfiguration.Embedded(sourcePath),
                null,
                CreateSqlConfiguration(destinationDatabaseName),
                null,
                Guid.NewGuid(),
                NewContext());

            Assert.False(result.IsSuccess);
            Assert.Equal(
                "hive.persistence.data-migration.schema-incomplete",
                result.Error!.Code);
        }
        finally
        {
            TryDeleteDirectory(sourceDirectory);
            await DropSqlDatabaseAsync(destinationDatabaseName);
        }
    }

    [Fact]
    public async Task Migration_RollsBackDestinationWhenTransferFails()
    {
        var sourceDatabaseName = $"Hive_Test_Migration_RollbackSource_{Guid.NewGuid():N}";
        var embeddedDirectory = CreateEmbeddedDirectory("rollback-destination");
        var embeddedPath = Path.Combine(embeddedDirectory, "hive.db");

        try
        {
            var sourceOptions = await CreateSqlDatabaseAsync(sourceDatabaseName);
            var sourceContext = NewContext();
            var provider = CreateProvider(sourceContext, "rollback");
            var providerStore = new SqlProviderResourceStore(sourceOptions);
            var created = await providerStore.CreateProviderAsync(
                provider,
                sourceContext);
            Assert.True(created.IsSuccess, created.Error?.Message);

            await using var destination = new EmbeddedPersistenceDatabase(
                HivePersistenceConfiguration.Embedded(embeddedPath));
            var initialization = await destination.InitializeAsync();
            Assert.True(initialization.IsSuccess, initialization.Error?.Message);

            await using (var connection = await destination.OpenConnectionAsync())
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TRIGGER [HiveMigrationFailureProbe]
                    AFTER INSERT ON [HiveProviders]
                    BEGIN
                        SELECT RAISE(ABORT, 'migration probe');
                    END;
                    """;
                await command.ExecuteNonQueryAsync();
            }

            var result = await new HivePersistenceDataMigrator().MigrateAsync(
                CreateSqlConfiguration(sourceDatabaseName),
                null,
                HivePersistenceConfiguration.Embedded(embeddedPath),
                null,
                Guid.NewGuid(),
                sourceContext);

            Assert.False(result.IsSuccess);
            Assert.Equal(
                "hive.persistence.data-migration.failed",
                result.Error!.Code);

            Assert.Equal(
                0,
                await CountEmbeddedRowsAsync(
                    destination,
                    "HiveProviders"));
            Assert.Equal(
                0,
                await CountEmbeddedRowsAsync(
                    destination,
                    "HiveProviderAccounts"));
        }
        finally
        {
            await DropSqlDatabaseAsync(sourceDatabaseName);
            TryDeleteDirectory(embeddedDirectory);
        }
    }

    [Fact]
    public async Task Migration_RejectsExtraEmbeddedSourceTables()
    {
        var sourceDirectory = CreateEmbeddedDirectory("extra-source");
        var sourcePath = Path.Combine(sourceDirectory, "hive.db");
        var destinationDatabaseName = $"Hive_Test_Migration_ExtraSourceDestination_{Guid.NewGuid():N}";

        try
        {
            var context = NewContext();
            await using var source = new EmbeddedPersistenceDatabase(
                HivePersistenceConfiguration.Embedded(sourcePath));
            var initialization = await source.InitializeAsync();
            Assert.True(initialization.IsSuccess, initialization.Error?.Message);

            await using (var connection = await source.OpenConnectionAsync())
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE [HiveUnexpectedTable]
                    (
                        [Id] INTEGER NOT NULL
                    );
                    """;
                await command.ExecuteNonQueryAsync();
            }

            _ = await CreateSqlDatabaseAsync(destinationDatabaseName);

            var result = await new HivePersistenceDataMigrator().MigrateAsync(
                HivePersistenceConfiguration.Embedded(sourcePath),
                null,
                CreateSqlConfiguration(destinationDatabaseName),
                null,
                Guid.NewGuid(),
                context);

            Assert.False(result.IsSuccess);
            Assert.Equal(
                "hive.persistence.data-migration.embedded-schema-incompatible",
                result.Error!.Code);
        }
        finally
        {
            await DropSqlDatabaseAsync(destinationDatabaseName);
            TryDeleteDirectory(sourceDirectory);
        }
    }

    [Fact]
    public async Task Migration_CancellationBeforeExecutionLeavesDestinationUntouched()
    {
        var sourceDatabaseName = $"Hive_Test_Migration_CancelSource_{Guid.NewGuid():N}";
        var destinationDirectory = CreateEmbeddedDirectory("cancel");
        var destinationPath = Path.Combine(destinationDirectory, "hive.db");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            _ = await CreateSqlDatabaseAsync(sourceDatabaseName);

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => new HivePersistenceDataMigrator().MigrateAsync(
                    CreateSqlConfiguration(sourceDatabaseName),
                    null,
                    HivePersistenceConfiguration.Embedded(destinationPath),
                    null,
                    Guid.NewGuid(),
                    NewContext(),
                    cancellation.Token));
        }
        finally
        {
            await DropSqlDatabaseAsync(sourceDatabaseName);
            TryDeleteDirectory(destinationDirectory);
        }
    }

    private static async Task<SqlSeed> SeedRepresentativeSqlDataAsync(
        HiveDatabaseOptions options)
    {
        var now = new DateTimeOffset(
            2030,
            1,
            2,
            3,
            4,
            5,
            1234567,
            TimeSpan.Zero);
        var context = NewContext();
        var providerStore = new SqlProviderResourceStore(options);
        var agentDefinitionStore = new SqlAgentDefinitionResourceStore(options);
        var workItemStore = new SqlWorkItemResourceStore(options);
        var favorites = new SqlExecutionTargetPreferenceStore(options);
        var events = HiveEventPersistence.CreateSql(options).EventStore;
        var agentWork = HiveAgentWorkPersistence.CreateSql(options);
        var secrets = new SqlDpapiSecretStore(options);

        var secretId = SecretId.New();
        var secret = new Secret(
            new ResourceEnvelope<SecretId>(
                ResourceKind.Secret,
                secretId,
                context.PrincipalId!.Value,
                ResourceScope.Tenant(context.TenantId!.Value),
                ResourceVersion.Initial,
                Provenance(context.PrincipalId.Value, now),
                ResourceLifecycle.Active(now)),
            "migration-secret",
            "Migration Secret");

        const string secretValue = "migration-secret-value";
        using (var material = SecretMaterial.Create(secretValue))
        {
            var createdSecret = await secrets.CreateAsync(
                secret,
                material,
                context);
            Assert.True(
                createdSecret.IsSuccess,
                createdSecret.Error?.Message);
        }

        var provider = CreateProvider(context, "complete");
        var providerResult = await providerStore.CreateProviderAsync(
            provider,
            context);
        Assert.True(providerResult.IsSuccess, providerResult.Error?.Message);

        var account = new ProviderAccount(
            new ResourceEnvelope<ProviderAccountId>(
                ResourceKind.ProviderAccount,
                ProviderAccountId.New(),
                context.PrincipalId!.Value,
                ResourceScope.Tenant(context.TenantId!.Value),
                ResourceVersion.Initial,
                Provenance(context.PrincipalId.Value, now),
                ResourceLifecycle.Active(now)),
            provider.Id,
            "migration-account",
            "Migration Account",
            "migration-external-account",
            new SecretReference(secretId));

        var accountResult = await providerStore.CreateProviderAccountAsync(
            account,
            context);
        Assert.True(accountResult.IsSuccess, accountResult.Error?.Message);

        var firstTarget = CreateTarget(
            provider.Id,
            account.Id,
            context.PrincipalId.Value,
            context.TenantId!.Value,
            "first",
            now);
        var secondTarget = CreateTarget(
            provider.Id,
            account.Id,
            context.PrincipalId.Value,
            context.TenantId.Value,
            "second",
            now);

        Assert.True(
            (await providerStore.CreateExecutionTargetAsync(
                firstTarget,
                context)).IsSuccess);
        Assert.True(
            (await providerStore.CreateExecutionTargetAsync(
                secondTarget,
                context)).IsSuccess);

        var favoriteResult = await favorites.ReplaceFavoriteExecutionTargetIdsAsync(
            [secondTarget.Id, firstTarget.Id],
            context);
        Assert.True(favoriteResult.IsSuccess, favoriteResult.Error?.Message);

        var agentDefinition = new AgentDefinition(
            new ResourceEnvelope<AgentDefinitionId>(
                ResourceKind.AgentDefinition,
                AgentDefinitionId.New(),
                context.PrincipalId.Value,
                ResourceScope.Tenant(context.TenantId.Value),
                ResourceVersion.Initial,
                Provenance(context.PrincipalId.Value, now),
                ResourceLifecycle.Active(now)),
            "migration-agent",
            "Migration Agent",
            AgentGeneration.Base,
            secondTarget.Id);

        var definitionResult =
            await agentDefinitionStore.CreateAgentDefinitionAsync(
                agentDefinition,
                context);
        Assert.True(
            definitionResult.IsSuccess,
            definitionResult.Error?.Message);

        var attachmentContent = new byte[] { 9, 8, 7, 6, 5 };
        var workItemResult = await workItemStore.CreateImageWorkItemAsync(
            new WorkItemImageSubmission(
                "migration.png",
                "image/png",
                attachmentContent),
            context);
        Assert.True(workItemResult.IsSuccess, workItemResult.Error?.Message);

        var eventStream = new ResourceReference(
            ResourceKind.WorkItem,
            workItemResult.Value!.Id.Value);
        var eventId = EventId.New();
        var eventOccurredAt = now.AddMinutes(2);
        var envelope = new JsonEventSerializer().CreateEnvelope(
            eventId,
            eventOccurredAt,
            new EventType("migration.representative"),
            new EventPayloadVersion(1),
            CorrelationId.New(),
            null,
            new { state = "Migrated" });

        var append = await events.AppendAsync(
            new EventAppendRequest(
                eventStream,
                new ResourceVersion(1),
                envelope,
                new EventSnapshot(
                    eventStream,
                    new ResourceVersion(2),
                    new EventPayloadVersion(1),
                    JsonSerializer.SerializeToElement(
                        new { state = "Migrated" }))));
        Assert.True(append.IsSuccess, append.Error?.Message);

        var agent = new AgentFactory(
            new AllowBaseAgentCreationAuthorizer(),
            new FixedClock(now))
            .Create<Agent>(
                new AgentDefinition(
                    "migration-work-agent",
                    "Migration Work Agent"),
                new AgentCreationContext(context))
            .Value!;

        var runtimeId = RuntimeId.New();
        var runtime = agent.CreateRuntimeInstance(
            now,
            clock: new FixedClock(now),
            workStores: agentWork,
            runtimeId: runtimeId);
        var runtimeContext = new ResourceAccessContext(
            context.DeploymentId,
            context.TenantId,
            context.PrincipalId,
            AgentId: agent.Id,
            RuntimeId: runtimeId);

        var workItem = WorkItem.Create(
            WorkItemId.New(),
            context.PrincipalId.Value,
            ResourceScope.Runtime(runtimeId),
            Provenance(context.PrincipalId.Value, now),
            now);

        var binding = runtime.Work.BindWorkItem(
            runtimeContext,
            workItem,
            now,
            CorrelationId.New());
        Assert.True(binding.IsSuccess, binding.Error?.Message);

        var objective = runtime.Work.Objectives.Create(
            runtimeContext,
            agent.Id,
            runtimeId,
            "Migration objective",
            new ObjectiveUpdate(
                "The representative dataset remains durable.",
                10,
                null,
                []),
            now,
            binding.Value);
        Assert.True(objective.IsSuccess, objective.Error?.Message);

        var memory = runtime.Work.Memory.Store(
            runtimeContext,
            agent.Id,
            runtimeId,
            "migration-memory",
            "durable-memory",
            now,
            evidenceKind: MemoryEvidenceKind.Actual);
        Assert.True(memory.IsSuccess, memory.Error?.Message);

        return new SqlSeed(
            context,
            secretId,
            secretValue,
            account.Id,
            [secondTarget.Id, firstTarget.Id],
            workItemResult.Value.Id,
            attachmentContent,
            eventStream,
            eventId,
            new ResourceVersion(2),
            agent,
            runtimeId,
            objective.Value!.Id,
            memory.Value!.Id,
            "durable-memory",
            now);
    }

    private static async Task<HiveDatabaseOptions> CreateSqlDatabaseAsync(
        string databaseName)
    {
        var database = new PersistenceTestDatabase(databaseName);
        database.Reset();

        var migration = await new HiveDatabaseMigrator(
            database.Options).MigrateAsync();
        Assert.True(
            migration.IsSuccess,
            migration.Error?.Message);

        return database.Options;
    }

    private static async Task<Dictionary<string, long>> ReadSqlCountsAsync(
        HiveDatabaseOptions options)
    {
        await using var connection = new SqlConnection(
            options.ConnectionString);
        await connection.OpenAsync();

        var counts = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var table in MigrationTableNames)
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"SELECT COUNT_BIG(1) FROM [dbo].[{table}];";
            counts[table] = Convert.ToInt64(
                await command.ExecuteScalarAsync());
        }

        return counts;
    }

    private static async Task<Dictionary<string, long>> ReadEmbeddedCountsAsync(
        EmbeddedPersistenceDatabase database)
    {
        await using var connection = await database.OpenConnectionAsync();
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var table in MigrationTableNames)
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"SELECT COUNT(*) FROM [{table}];";
            counts[table] = Convert.ToInt64(
                await command.ExecuteScalarAsync());
        }

        return counts;
    }

    private static void AssertCountsEqual(
        IReadOnlyDictionary<string, long> expected,
        IReadOnlyDictionary<string, long> actual)
    {
        Assert.Equal(
            expected.Keys.OrderBy(static value => value, StringComparer.Ordinal),
            actual.Keys.OrderBy(static value => value, StringComparer.Ordinal));

        foreach (var key in expected.Keys)
            Assert.Equal(expected[key], actual[key]);
    }

    private static async Task<long> CountSqlRowsAsync(
        HiveDatabaseOptions options,
        string table)
    {
        await using var connection = new SqlConnection(
            options.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT COUNT_BIG(1) FROM [dbo].[{table}];";

        return Convert.ToInt64(
            await command.ExecuteScalarAsync());
    }

    private static async Task<long> CountEmbeddedRowsAsync(
        EmbeddedPersistenceDatabase database,
        string table)
    {
        await using var connection = await database.OpenConnectionAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT COUNT(*) FROM [{table}];";

        return Convert.ToInt64(
            await command.ExecuteScalarAsync());
    }

    private static HivePersistenceConfiguration CreateSqlConfiguration(
        string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(
            HivePersistenceTestConfiguration.ConnectionString);

        var serverName = builder.DataSource;
        int? port = null;
        var separator = serverName.LastIndexOf(",", StringComparison.Ordinal);

        if (separator > 0 &&
            int.TryParse(
                serverName[(separator + 1)..],
                out var parsedPort))
        {
            serverName = serverName[..separator];
            port = parsedPort;
        }

        if (!builder.IntegratedSecurity)
        {
            throw new InvalidOperationException(
                "Hive persistence migration tests require Windows integrated SQL authentication.");
        }

        return new HivePersistenceConfiguration(
            HivePersistenceBackend.SqlServer,
            serverName,
            port,
            databaseName,
            HiveSqlAuthenticationMode.WindowsIntegrated,
            null,
            null,
            builder.Encrypt,
            builder.TrustServerCertificate,
            createDatabaseIfMissing: true);
    }

    private static ResourceAccessContext NewContext() =>
        new(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());

    private static Provider CreateProvider(
        ResourceAccessContext context,
        string suffix) =>
        new(
            new ResourceEnvelope<ProviderId>(
                ResourceKind.Provider,
                ProviderId.New(),
                context.PrincipalId!.Value,
                ResourceScope.Tenant(context.TenantId!.Value),
                ResourceVersion.Initial,
                Provenance(
                    context.PrincipalId.Value,
                    new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero)),
                ResourceLifecycle.Active(
                    new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero))),
            $"migration-provider-{suffix}-{Guid.NewGuid():N}",
            "Migration Provider",
            "openai-compatible");

    private static ExecutionTarget CreateTarget(
        ProviderId providerId,
        ProviderAccountId accountId,
        PrincipalId owner,
        TenantId tenant,
        string suffix,
        DateTimeOffset now) =>
        new(
            new ResourceEnvelope<ExecutionTargetId>(
                ResourceKind.ExecutionTarget,
                ExecutionTargetId.New(),
                owner,
                ResourceScope.Tenant(tenant),
                ResourceVersion.Initial,
                Provenance(owner, now),
                ResourceLifecycle.Active(now)),
            providerId,
            accountId,
            $"migration-target-{suffix}-{Guid.NewGuid():N}",
            $"Migration Target {suffix}",
            new Uri("https://example.test/v1"),
            $"migration-model-{suffix}",
            null,
            [
                new CapabilityStateEntry(
                    HiveCapabilityKeys.TextGeneration,
                    CapabilityState.Supported)
            ]);

    private static ResourceProvenance Provenance(
        PrincipalId principal,
        DateTimeOffset now) =>
        new(
            principal,
            now,
            CorrelationId.New());

    private static string CreateEmbeddedDirectory(string name)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "Hive.Tests",
            "Embedded",
            $"Migration_{name}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static async Task DropSqlDatabaseAsync(
        string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            return;

        try
        {
            var builder = new SqlConnectionStringBuilder(
                HivePersistenceTestConfiguration.ConnectionString)
            {
                InitialCatalog = "master",
                ApplicationName = "Hive.Tests"
            };

            await using var connection = new SqlConnection(
                builder.ConnectionString);
            await connection.OpenAsync();

            var quoted = $"[{databaseName.Replace("]", "]]", StringComparison.Ordinal)}]";

            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                IF DB_ID(@DatabaseName) IS NOT NULL
                BEGIN
                    ALTER DATABASE {quoted} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE {quoted};
                END;
                """;
            command.Parameters.Add(
                new SqlParameter("@DatabaseName", databaseName));

            await command.ExecuteNonQueryAsync();
        }
        catch (SqlException)
        {
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch
        {
        }
    }

    private static readonly string[] MigrationTableNames =
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

    private sealed record SqlSeed(
        ResourceAccessContext Context,
        SecretId SecretId,
        string SecretValue,
        ProviderAccountId ProviderAccountId,
        IReadOnlyList<ExecutionTargetId> FavoriteTargetIds,
        WorkItemId WorkItemId,
        byte[] AttachmentContent,
        ResourceReference EventStream,
        EventId EventId,
        ResourceVersion EventSnapshotVersion,
        Agent Agent,
        RuntimeId RuntimeId,
        ObjectiveId ObjectiveId,
        MemoryId MemoryId,
        string MemoryContent,
        DateTimeOffset Now);

    private sealed class TestConfigurationStore : IHiveConfigurationStore
    {
        private readonly HivePersistenceConfiguration _configuration;

        public TestConfigurationStore(
            HivePersistenceConfiguration configuration) =>
            _configuration = configuration;

        public Task<Result<HivePersistenceConfiguration>> LoadPersistenceConfigurationAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                Result<HivePersistenceConfiguration>.Success(_configuration));

        public Task<Result<HivePersistenceConfiguration>> SavePersistenceConfigurationAsync(
            HivePersistenceConfiguration configuration,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                Result<HivePersistenceConfiguration>.Success(configuration));
    }

    private sealed class RecordingQuiescence : IHivePersistenceMigrationQuiescence
    {
        public Error? Failure { get; init; }
        public bool Acquired { get; private set; }
        public bool Released { get; private set; }

        public Task<Result<IAsyncDisposable>> AcquireAsync(
            CancellationToken cancellationToken = default)
        {
            Acquired = true;

            if (Failure is not null)
            {
                return Task.FromResult(
                    Result<IAsyncDisposable>.Failure(Failure));
            }

            return Task.FromResult(
                Result<IAsyncDisposable>.Success(
                    new Lease(() => Released = true)));
        }

        private sealed class Lease : IAsyncDisposable
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

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow) =>
            UtcNow = utcNow;

        public DateTimeOffset UtcNow { get; }
    }
}
