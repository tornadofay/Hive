using System.Text.Json;
using Hive.Agents;
using Hive.Core;
using Hive.Persistence;
using Hive.Tests.TestInfrastructure;
using Xunit;

namespace Hive.Tests;

public sealed class EmbeddedPersistenceParityTests
{
    [Fact]
    public async Task CoreResourceContracts_AreSatisfiedBySqlServerAndEmbedded()
    {
        await using var sql = await CreateSqlBackendAsync("Hive_Test_EmbeddedParity_Core");
        await ExerciseCoreResourcesAsync(sql);

        await using var embedded = await CreateEmbeddedBackendAsync("core");
        await ExerciseCoreResourcesAsync(embedded);
    }

    [Fact]
    public async Task EventContracts_AreSatisfiedBySqlServerAndEmbedded()
    {
        await using var sql = await CreateSqlBackendAsync("Hive_Test_EmbeddedParity_Events");
        await ExerciseEventsAsync(sql);

        await using var embedded = await CreateEmbeddedBackendAsync("events");
        await ExerciseEventsAsync(embedded);
    }

    [Fact]
    public async Task AgentWorkContracts_AreSatisfiedBySqlServerAndEmbedded()
    {
        await using var sql = await CreateSqlBackendAsync("Hive_Test_EmbeddedParity_AgentWork");
        await ExerciseAgentWorkAsync(sql);

        await using var embedded = await CreateEmbeddedBackendAsync("agent-work");
        await ExerciseAgentWorkAsync(embedded);
    }

    private static async Task ExerciseCoreResourcesAsync(Backend backend)
    {
        var now = new DateTimeOffset(
            2030,
            1,
            2,
            3,
            4,
            5,
            TimeSpan.Zero);
        var deployment = DeploymentId.New();
        var tenant = TenantId.New();
        var principal = PrincipalId.New();
        var context = new ResourceAccessContext(
            deployment,
            tenant,
            principal);

        var provider = new Provider(
            new ResourceEnvelope<ProviderId>(
                ResourceKind.Provider,
                ProviderId.New(),
                principal,
                ResourceScope.Tenant(tenant),
                ResourceVersion.Initial,
                Provenance(principal, now),
                ResourceLifecycle.Active(now)),
            $"parity-provider-{Guid.NewGuid():N}",
            "Parity Provider",
            "openai-compatible");

        var createdProvider = await backend.Providers.CreateProviderAsync(
            provider,
            context);
        Assert.True(createdProvider.IsSuccess, createdProvider.Error?.Message);

        var account = new ProviderAccount(
            new ResourceEnvelope<ProviderAccountId>(
                ResourceKind.ProviderAccount,
                ProviderAccountId.New(),
                principal,
                ResourceScope.Tenant(tenant),
                ResourceVersion.Initial,
                Provenance(principal, now),
                ResourceLifecycle.Active(now)),
            provider.Id,
            $"parity-account-{Guid.NewGuid():N}",
            "Parity Account",
            "parity-account");

        var createdAccount = await backend.Providers.CreateProviderAccountAsync(
            account,
            context);
        Assert.True(createdAccount.IsSuccess, createdAccount.Error?.Message);

        var firstTarget = CreateTarget(
            provider.Id,
            account.Id,
            principal,
            tenant,
            "first",
            now);
        var secondTarget = CreateTarget(
            provider.Id,
            account.Id,
            principal,
            tenant,
            "second",
            now);

        Assert.True(
            (await backend.Providers.CreateExecutionTargetAsync(
                firstTarget,
                context)).IsSuccess);
        Assert.True(
            (await backend.Providers.CreateExecutionTargetAsync(
                secondTarget,
                context)).IsSuccess);

        var favorites = await backend.Favorites.ReplaceFavoriteExecutionTargetIdsAsync(
            [secondTarget.Id, firstTarget.Id],
            context);
        Assert.True(favorites.IsSuccess, favorites.Error?.Message);

        var reloadedFavorites = await backend.Favorites.GetFavoriteExecutionTargetIdsAsync(
            context);
        Assert.True(
            reloadedFavorites.IsSuccess,
            reloadedFavorites.Error?.Message);
        Assert.Equal(
            [secondTarget.Id, firstTarget.Id],
            reloadedFavorites.Value);

        var definition = new AgentDefinition(
            new ResourceEnvelope<AgentDefinitionId>(
                ResourceKind.AgentDefinition,
                AgentDefinitionId.New(),
                principal,
                ResourceScope.Tenant(tenant),
                ResourceVersion.Initial,
                Provenance(principal, now),
                ResourceLifecycle.Active(now)),
            "parity-agent",
            "Parity Agent",
            AgentGeneration.Base,
            secondTarget.Id);

        var createdDefinition =
            await backend.AgentDefinitions.CreateAgentDefinitionAsync(
                definition,
                context);
        Assert.True(
            createdDefinition.IsSuccess,
            createdDefinition.Error?.Message);

        var reloadedDefinition =
            await backend.AgentDefinitions.GetAgentDefinitionAsync(
                definition.Id,
                context);
        Assert.True(
            reloadedDefinition.IsSuccess,
            reloadedDefinition.Error?.Message);
        Assert.Equal(
            secondTarget.Id,
            reloadedDefinition.Value!.ConfiguredExecutionTargetId);

        var createdWorkItem = await backend.WorkItems.CreateImageWorkItemAsync(
            new WorkItemImageSubmission(
                "parity.png",
                "image/png",
                new byte[] { 1, 2, 3, 4, 5 }),
            context);
        Assert.True(createdWorkItem.IsSuccess, createdWorkItem.Error?.Message);
        Assert.NotNull(createdWorkItem.Value!.Attachment);

        var reloadedWorkItem = await backend.WorkItems.GetWorkItemAsync(
            createdWorkItem.Value!.Id,
            context);
        Assert.True(
            reloadedWorkItem.IsSuccess,
            reloadedWorkItem.Error?.Message);
        Assert.Equal(
            createdWorkItem.Value!.Id,
            reloadedWorkItem.Value!.Id);

        var attachment = await backend.WorkItems.GetWorkItemAttachmentAsync(
            createdWorkItem.Value!.Id,
            context);
        Assert.True(attachment.IsSuccess, attachment.Error?.Message);
        Assert.Equal(
            [1, 2, 3, 4, 5],
            attachment.Value!.Content.ToArray());
    }

    private static async Task ExerciseEventsAsync(Backend backend)
    {
        var stream = new ResourceReference(
            ResourceKind.WorkItem,
            Guid.NewGuid());
        var occurredAt = new DateTimeOffset(
            2030,
            1,
            2,
            3,
            4,
            5,
            TimeSpan.Zero);

        var envelope = new JsonEventSerializer().CreateEnvelope(
            EventId.New(),
            occurredAt,
            new EventType("parity.created"),
            new EventPayloadVersion(1),
            CorrelationId.New(),
            null,
            new { state = "Created" });

        var append = await backend.Events.AppendAsync(
            new EventAppendRequest(
                stream,
                null,
                envelope,
                new EventSnapshot(
                    stream,
                    ResourceVersion.Initial,
                    new EventPayloadVersion(1),
                    JsonSerializer.SerializeToElement(
                        new { state = "Created" }))));

        Assert.True(append.IsSuccess, append.Error?.Message);
        Assert.Equal(
            ResourceVersion.Initial,
            append.Value!.Event.StreamVersion);

        var events = await backend.Events.ReadEventsAsync(stream);
        Assert.True(events.IsSuccess, events.Error?.Message);
        Assert.Single(events.Value!);
        Assert.Equal(
            envelope.EventId,
            events.Value![0].Envelope.EventId);

        var snapshot = await backend.Events.GetSnapshotAsync(stream);
        Assert.True(snapshot.IsSuccess, snapshot.Error?.Message);
        Assert.Equal(1, snapshot.Value!.Version.Value);
        Assert.Equal(
            "Created",
            snapshot.Value.State.GetProperty("state").GetString());

        var claimed = await backend.Events.ClaimNextOutboxAsync(
            TimeSpan.FromMinutes(5));
        Assert.True(claimed.IsSuccess, claimed.Error?.Message);
        Assert.NotNull(claimed.Value);
        Assert.Equal(1, claimed.Value!.AttemptCount);

        var renewed = await backend.Events.RenewOutboxLeaseAsync(
            claimed.Value,
            TimeSpan.FromMinutes(5));
        Assert.True(renewed.IsSuccess, renewed.Error?.Message);

        var completed = await backend.Events.CompleteOutboxAsync(
            claimed.Value);
        Assert.True(completed.IsSuccess, completed.Error?.Message);

        var missing = await backend.Events.GetOutboxAsync(
            envelope.EventId);
        Assert.True(missing.IsSuccess, missing.Error?.Message);
        Assert.Null(missing.Value);
    }

    private static async Task ExerciseAgentWorkAsync(Backend backend)
    {
        var now = new DateTimeOffset(
            2030,
            1,
            2,
            7,
            0,
            0,
            TimeSpan.Zero);
        var clock = new FixedClock(now);
        var deployment = DeploymentId.New();
        var tenant = TenantId.New();
        var principal = PrincipalId.New();
        var baseContext = new ResourceAccessContext(
            deployment,
            tenant,
            principal);

        var agent = new AgentFactory(
            new AllowBaseAgentCreationAuthorizer(),
            clock)
            .Create<Agent>(
                new AgentDefinition(
                    "parity-work-agent",
                    "Parity Work Agent"),
                new AgentCreationContext(baseContext))
            .Value!;

        var runtimeId = RuntimeId.New();
        var runtime = agent.CreateRuntimeInstance(
            now,
            clock: clock,
            workStores: backend.AgentWork,
            runtimeId: runtimeId);

        var context = new ResourceAccessContext(
            deployment,
            tenant,
            principal,
            AgentId: agent.Id,
            RuntimeId: runtimeId);

        var workItem = WorkItem.Create(
            WorkItemId.New(),
            principal,
            ResourceScope.Runtime(runtimeId),
            Provenance(principal, now),
            now);

        var binding = runtime.Work.BindWorkItem(
            context,
            workItem,
            now,
            CorrelationId.New());
        Assert.True(binding.IsSuccess, binding.Error?.Message);

        var objective = runtime.Work.Objectives.Create(
            context,
            agent.Id,
            runtimeId,
            "Parity objective",
            new ObjectiveUpdate(
                "The persistence contract remains durable.",
                10,
                null,
                []),
            now,
            binding.Value);
        Assert.True(objective.IsSuccess, objective.Error?.Message);

        var storedMemory = runtime.Work.Memory.Store(
            context,
            agent.Id,
            runtimeId,
            "parity",
            "embedded-or-sql",
            now,
            evidenceKind: MemoryEvidenceKind.Actual);
        Assert.True(storedMemory.IsSuccess, storedMemory.Error?.Message);

        var recovered = agent.CreateRuntimeInstance(
            now.AddMinutes(1),
            clock: clock,
            workStores: backend.AgentWork,
            runtimeId: runtimeId);

        var recoveredObjective = recovered.Work.Objectives.Get(
            context,
            agent.Id,
            runtimeId,
            objective.Value!.Id);
        Assert.True(
            recoveredObjective.IsSuccess,
            recoveredObjective.Error?.Message);
        Assert.Equal(
            objective.Value!.Id,
            recoveredObjective.Value!.Id);

        var recoveredMemory = recovered.Work.Memory.Get(
            context,
            agent.Id,
            runtimeId,
            storedMemory.Value!.Id);
        Assert.True(
            recoveredMemory.IsSuccess,
            recoveredMemory.Error?.Message);
        Assert.Equal(
            "embedded-or-sql",
            recoveredMemory.Value!.Content);
    }

    private static async Task<Backend> CreateSqlBackendAsync(string name)
    {
        var database = new PersistenceTestDatabase(
            $"{name}_{Guid.NewGuid():N}");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(
            database.Options).MigrateAsync();

        Assert.True(
            migration.IsSuccess,
            migration.Error?.Message);

        var events = HiveEventPersistence.CreateSql(
            database.Options);

        return new Backend(
            new SqlProviderResourceStore(database.Options),
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            new SqlExecutionTargetPreferenceStore(database.Options),
            events.EventStore,
            HiveAgentWorkPersistence.CreateSql(database.Options),
            null,
            null);
    }

    private static async Task<Backend> CreateEmbeddedBackendAsync(string name)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "Hive.Tests",
            "Embedded",
            $"{name}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, "hive.db");
        var database = new EmbeddedPersistenceDatabase(
            HivePersistenceConfiguration.Embedded(path));

        try
        {
            var migration = await database.InitializeAsync();
            Assert.True(
                migration.IsSuccess,
                migration.Error?.Message);

            var events = HiveEventPersistence.CreateEmbedded(database);

            return new Backend(
                new EmbeddedProviderResourceStore(database),
                new EmbeddedAgentDefinitionResourceStore(database),
                new EmbeddedWorkItemResourceStore(database),
                new EmbeddedExecutionTargetPreferenceStore(database),
                events.EventStore,
                HiveAgentWorkPersistence.CreateEmbedded(database),
                database,
                directory);
        }
        catch
        {
            await database.DisposeAsync();
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch
            {
            }

            throw;
        }
    }

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
            $"target-{suffix}-{Guid.NewGuid():N}",
            $"Target {suffix}",
            new Uri("https://example.test/v1"),
            $"model-{suffix}",
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

    private sealed class Backend : IAsyncDisposable
    {
        public Backend(
            IProviderResourceStore providers,
            IAgentDefinitionResourceStore agentDefinitions,
            IWorkItemResourceStore workItems,
            IExecutionTargetPreferenceStore favorites,
            IEventPersistenceStore events,
            RuntimeWorkProtocolStores agentWork,
            EmbeddedPersistenceDatabase? embeddedDatabase,
            string? embeddedDirectory)
        {
            Providers = providers;
            AgentDefinitions = agentDefinitions;
            WorkItems = workItems;
            Favorites = favorites;
            Events = events;
            AgentWork = agentWork;
            EmbeddedDatabase = embeddedDatabase;
            EmbeddedDirectory = embeddedDirectory;
        }

        public IProviderResourceStore Providers { get; }
        public IAgentDefinitionResourceStore AgentDefinitions { get; }
        public IWorkItemResourceStore WorkItems { get; }
        public IExecutionTargetPreferenceStore Favorites { get; }
        public IEventPersistenceStore Events { get; }
        public RuntimeWorkProtocolStores AgentWork { get; }
        private EmbeddedPersistenceDatabase? EmbeddedDatabase { get; }
        private string? EmbeddedDirectory { get; }
        public async ValueTask DisposeAsync()
        {
            if (EmbeddedDatabase is not null)
                await EmbeddedDatabase.DisposeAsync();

            if (EmbeddedDirectory is not null)
            {
                try
                {
                    Directory.Delete(
                        EmbeddedDirectory,
                        recursive: true);
                }
                catch
                {
                }
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
