using Hive.Core;
using Hive.Persistence;
using Hive.Tests.TestInfrastructure;
using Hive.Management;
using Xunit;

namespace Hive.Tests;

public sealed class Phase116FollowUpManagementTests
{
    [Fact]
    public async Task EndpointScopedDiscovery_UsesTransientProbe_AndDoesNotCreateExecutionTarget()
    {
        var database = new PersistenceTestDatabase(
            "Hive_Test_Phase116FollowUp_EndpointDiscovery");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var context = new ResourceAccessContext(
            DeploymentId.New(),
            tenant,
            principal);

        var provider = CreateProvider(principal, tenant);
        var account = CreateAccount(provider.Id, principal, tenant);
        var endpoint = new Uri("https://example.test/v1/");
        var discovery = new RecordingDiscovery();

        var store = new SqlProviderResourceStore(database.Options);

        Assert.True(
            (await store.CreateProviderAsync(provider, context)).IsSuccess);
        Assert.True(
            (await store.CreateProviderAccountAsync(account, context)).IsSuccess);

        using var facade = new HiveManagementFacade(
            store,
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            providerCapabilityDiscovery: discovery);

        var result = await facade.GetProviderDiscoveryAsync(
            provider.Id,
            account.Id,
            endpoint,
            context);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Single(result.Value!.Models);
        Assert.Equal(endpoint, result.Value.Endpoint);

        Assert.Equal(1, discovery.CallCount);
        Assert.Equal(provider.Id, discovery.ProviderIdSeen);
        Assert.Equal(account.Id, discovery.ProviderAccountIdSeen);
        Assert.Equal(endpoint, discovery.EndpointSeen);
        Assert.Null(discovery.ModelSeen);

        var targets = await store.ListExecutionTargetsAsync(
            account.Id,
            context);

        Assert.True(targets.IsSuccess, targets.Error?.Message);
        Assert.Empty(targets.Value!);
    }

    private static Provider CreateProvider(
        PrincipalId principal,
        TenantId tenant)
    {
        var now = DateTimeOffset.UtcNow;

        return new Provider(
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
            "phase116-followup-provider",
            "Phase 1.16 Follow-Up Provider",
            "openai-compatible");
    }

    private static ProviderAccount CreateAccount(
        ProviderId providerId,
        PrincipalId principal,
        TenantId tenant)
    {
        var now = DateTimeOffset.UtcNow;

        return new ProviderAccount(
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
            providerId,
            "phase116-followup-account",
            "Phase 1.16 Follow-Up Account",
            "fixture-account");
    }

    private sealed class RecordingDiscovery : IProviderCapabilityDiscovery
    {
        public int CallCount { get; private set; }

        public ProviderId ProviderIdSeen { get; private set; }

        public ProviderAccountId ProviderAccountIdSeen { get; private set; }

        public Uri? EndpointSeen { get; private set; }

        public string? ModelSeen { get; private set; }

        public Task<Result<ProviderDiscoverySnapshot>> DiscoverAsync(
            Provider provider,
            ProviderAccount account,
            ExecutionTarget target,
            SecretMaterial? credential,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CallCount++;
            ProviderIdSeen = provider.Id;
            ProviderAccountIdSeen = account.Id;
            EndpointSeen = target.Endpoint;
            ModelSeen = target.Model;

            var now = new DateTimeOffset(
                2030,
                1,
                2,
                3,
                4,
                5,
                TimeSpan.Zero);

            var model = new ProviderModelMetadata(
                "rich-model",
                "fixture",
                now,
                ProviderAvailabilityStatus.Available,
                ProviderHealthStatus.Healthy,
                [
                    new CapabilityStateEntry(
                        HiveCapabilityKeys.TextGeneration,
                        CapabilityState.Supported)
                ],
                ["text"],
                ["text"],
                family: "fixture-family",
                modelType: "chat",
                category: "text",
                version: "1.0",
                operationalState: "active",
                observedAtUtc: now,
                staleAfterUtc: now.AddHours(1));

            var snapshot = new ProviderDiscoverySnapshot(
                provider.Id,
                account.Id,
                target.Endpoint,
                new ProviderOperationalMetadata(
                    ProviderAvailabilityStatus.Available,
                    ProviderHealthStatus.Healthy,
                    now,
                    now.AddHours(1)),
                ProviderDiscoveryState.Supported,
                [model]);

            return Task.FromResult(
                Result<ProviderDiscoverySnapshot>.Success(snapshot));
        }
    }
}
