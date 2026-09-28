using Hive.Core;
using Hive.Management;
using Hive.Persistence;
using Hive.Tests.TestInfrastructure;
using Xunit;

namespace Hive.Tests;

public sealed class ProviderDiscoveryManagementIntegrationTests
{
    [Fact]
    public async Task Management_DiscoveryIsCached_ForceRefreshes_AndEnforcesOwnership()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ProviderDiscoveryManagement");
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
        var target = CreateTarget(provider.Id, account.Id, principal, tenant);

        var providerStore = new SqlProviderResourceStore(database.Options);

        Assert.True(
            (await providerStore.CreateProviderAsync(provider, context)).IsSuccess);
        Assert.True(
            (await providerStore.CreateProviderAccountAsync(account, context)).IsSuccess);
        Assert.True(
            (await providerStore.CreateExecutionTargetAsync(target, context)).IsSuccess);

        var discovery = new RecordingDiscovery();
        using var facade = new HiveManagementFacade(
            providerStore,
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            providerCapabilityDiscovery: discovery);

        var first = await facade.GetProviderDiscoveryAsync(
            target.Id,
            context);

        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(1, discovery.CallCount);

        var cached = await facade.GetProviderDiscoveryAsync(
            target.Id,
            context);

        Assert.True(cached.IsSuccess, cached.Error?.Message);
        Assert.Equal(1, discovery.CallCount);

        var refreshed = await facade.GetProviderDiscoveryAsync(
            target.Id,
            context,
            forceRefresh: true);

        Assert.True(refreshed.IsSuccess, refreshed.Error?.Message);
        Assert.Equal(2, discovery.CallCount);
        Assert.Equal(
            ProviderDiscoveryState.Supported,
            refreshed.Value!.ModelEnumerationState);

        var forbidden = await facade.GetProviderDiscoveryAsync(
            target.Id,
            new ResourceAccessContext(
                context.DeploymentId,
                context.TenantId,
                PrincipalId.New()));

        Assert.True(forbidden.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, forbidden.Error!.Category);
        Assert.Equal(2, discovery.CallCount);
    }

    [Fact]
    public async Task Management_RejectsMismatchedDiscoveryResult_AndDoesNotCacheIt()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ProviderDiscoveryResultMismatch");
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
        var target = CreateTarget(provider.Id, account.Id, principal, tenant);

        var providerStore = new SqlProviderResourceStore(database.Options);

        Assert.True(
            (await providerStore.CreateProviderAsync(provider, context)).IsSuccess);
        Assert.True(
            (await providerStore.CreateProviderAccountAsync(account, context)).IsSuccess);
        Assert.True(
            (await providerStore.CreateExecutionTargetAsync(target, context)).IsSuccess);

        var discovery = new RecordingDiscovery(
            mismatchedResult: true);

        using var facade = new HiveManagementFacade(
            providerStore,
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            providerCapabilityDiscovery: discovery);

        var first = await facade.GetProviderDiscoveryAsync(
            target.Id,
            context);

        Assert.True(first.IsFailure);
        Assert.Equal(
            ErrorCategory.Validation,
            first.Error!.Category);
        Assert.Equal(
            "hive.management.provider-discovery-result-mismatch",
            first.Error.Code);
        Assert.Equal(1, discovery.CallCount);

        var second = await facade.GetProviderDiscoveryAsync(
            target.Id,
            context);

        Assert.True(second.IsFailure);
        Assert.Equal(
            ErrorCategory.Validation,
            second.Error!.Category);
        Assert.Equal(
            2,
            discovery.CallCount);
    }

    [Fact]
    public async Task Management_RejectsDiscoveryResultWithDifferentEndpointPathCase()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ProviderDiscoveryEndpointIdentity");
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
        var target = CreateTarget(provider.Id, account.Id, principal, tenant);
        var providerStore = new SqlProviderResourceStore(database.Options);

        Assert.True(
            (await providerStore.CreateProviderAsync(provider, context)).IsSuccess);
        Assert.True(
            (await providerStore.CreateProviderAccountAsync(account, context)).IsSuccess);
        Assert.True(
            (await providerStore.CreateExecutionTargetAsync(target, context)).IsSuccess);

        var discovery = new RecordingDiscovery(
            mismatchedEndpointPathCase: true);

        using var facade = new HiveManagementFacade(
            providerStore,
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            providerCapabilityDiscovery: discovery);

        var result = await facade.GetProviderDiscoveryAsync(
            target.Id,
            context);

        Assert.True(result.IsFailure);
        Assert.Equal(
            ErrorCategory.Validation,
            result.Error!.Category);
        Assert.Equal(
            "hive.management.provider-discovery-result-mismatch",
            result.Error.Code);
        Assert.Equal(1, discovery.CallCount);
    }

    [Fact]
    public async Task Management_ConcurrentDiscoveryRequestsShareOneInFlightDiscovery()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ProviderDiscoveryConcurrency");
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
        var target = CreateTarget(provider.Id, account.Id, principal, tenant);
        var providerStore = new SqlProviderResourceStore(database.Options);

        Assert.True(
            (await providerStore.CreateProviderAsync(provider, context)).IsSuccess);
        Assert.True(
            (await providerStore.CreateProviderAccountAsync(account, context)).IsSuccess);
        Assert.True(
            (await providerStore.CreateExecutionTargetAsync(target, context)).IsSuccess);

        var discovery = new RecordingDiscovery(
            delay: TimeSpan.FromMilliseconds(100));

        using var facade = new HiveManagementFacade(
            providerStore,
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            providerCapabilityDiscovery: discovery);

        var requests = Enumerable
            .Range(0, 8)
            .Select(_ => facade.GetProviderDiscoveryAsync(
                target.Id,
                context))
            .ToArray();

        var results = await Task.WhenAll(requests);

        Assert.All(
            results,
            result => Assert.True(result.IsSuccess, result.Error?.Message));
        Assert.Equal(1, discovery.CallCount);
    }

    [Fact]
    public async Task Management_FailedForceRefreshLeavesLastSuccessfulDiscoveryCached()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ProviderDiscoveryRefreshFailure");
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
        var target = CreateTarget(provider.Id, account.Id, principal, tenant);
        var providerStore = new SqlProviderResourceStore(database.Options);

        Assert.True(
            (await providerStore.CreateProviderAsync(provider, context)).IsSuccess);
        Assert.True(
            (await providerStore.CreateProviderAccountAsync(account, context)).IsSuccess);
        Assert.True(
            (await providerStore.CreateExecutionTargetAsync(target, context)).IsSuccess);

        var discovery = new RecordingDiscovery(
            failOnCall: 2);

        using var facade = new HiveManagementFacade(
            providerStore,
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            providerCapabilityDiscovery: discovery);

        var first = await facade.GetProviderDiscoveryAsync(
            target.Id,
            context);

        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(1, discovery.CallCount);

        var failedRefresh = await facade.GetProviderDiscoveryAsync(
            target.Id,
            context,
            forceRefresh: true);

        Assert.True(failedRefresh.IsFailure);
        Assert.Equal(ErrorCategory.External, failedRefresh.Error!.Category);
        Assert.Equal(2, discovery.CallCount);

        var cached = await facade.GetProviderDiscoveryAsync(
            target.Id,
            context);

        Assert.True(cached.IsSuccess, cached.Error?.Message);
        Assert.Equal(2, discovery.CallCount);
        Assert.Equal(
            first.Value!.Operational.ObservedAtUtc,
            cached.Value!.Operational.ObservedAtUtc);
    }

    [Fact]
    public async Task Management_InputPreparationRefreshesStaleDiscovery_AndUsesDiscoveredVision()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ProviderDiscoveryInput");
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
        var target = CreateTarget(provider.Id, account.Id, principal, tenant);

        var providerStore = new SqlProviderResourceStore(database.Options);

        Assert.True(
            (await providerStore.CreateProviderAsync(provider, context)).IsSuccess);
        Assert.True(
            (await providerStore.CreateProviderAccountAsync(account, context)).IsSuccess);
        Assert.True(
            (await providerStore.CreateExecutionTargetAsync(target, context)).IsSuccess);

        var discovery = new RecordingDiscovery(
            staleFirst: true);

        using var facade = new HiveManagementFacade(
            providerStore,
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            providerCapabilityDiscovery: discovery);

        var submission = new InputSubmission(
        [
            new InputItem(
                "discovery-image.png",
                "image/png",
                new byte[] { 1, 2, 3 })
        ]);

        var prepared = await facade.PrepareInputAsync(
            submission,
            context);

        Assert.True(prepared.IsSuccess, prepared.Error?.Message);

        var image = Assert.IsType<PreparedImageInput>(
            Assert.Single(prepared.Value!.PreparedInputs));

        Assert.Equal(target.Id, image.ExecutionTargetId);
        Assert.Equal(2, discovery.CallCount);
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
            $"provider-{Guid.NewGuid():N}",
            "Discovery Provider",
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
            $"account-{Guid.NewGuid():N}",
            "Discovery Account",
            "example-account");
    }

    private static ExecutionTarget CreateTarget(
        ProviderId providerId,
        ProviderAccountId accountId,
        PrincipalId principal,
        TenantId tenant)
    {
        var now = DateTimeOffset.UtcNow;

        return new ExecutionTarget(
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
            providerId,
            accountId,
            $"target-{Guid.NewGuid():N}",
            "Discovery Target",
            new Uri("https://discovery.example.test/v1/"),
            "vision-model",
            null,
            Array.Empty<CapabilityStateEntry>());
    }

    private sealed class RecordingDiscovery : IProviderCapabilityDiscovery
    {
        private readonly bool _staleFirst;
        private readonly int? _failOnCall;
        private readonly bool _mismatchedResult;
        private readonly bool _mismatchedEndpointPathCase;
        private readonly TimeSpan? _delay;
        private int _callCount;

        public RecordingDiscovery(
            bool staleFirst = false,
            int? failOnCall = null,
            bool mismatchedResult = false,
            bool mismatchedEndpointPathCase = false,
            TimeSpan? delay = null)
        {
            _staleFirst = staleFirst;
            _failOnCall = failOnCall;
            _mismatchedResult = mismatchedResult;
            _mismatchedEndpointPathCase = mismatchedEndpointPathCase;
            _delay = delay;
        }

        public int CallCount =>
            Volatile.Read(ref _callCount);

        public async Task<Result<ProviderDiscoverySnapshot>> DiscoverAsync(
            Provider provider,
            ProviderAccount account,
            ExecutionTarget target,
            SecretMaterial? credential,
            CancellationToken cancellationToken = default)
        {
            Assert.Null(credential);

            var callNumber = Interlocked.Increment(ref _callCount);

            if (_delay is { } delay)
            {
                await Task.Delay(
                    delay,
                    cancellationToken);
            }

            if (_failOnCall == callNumber)
            {
                return Result<ProviderDiscoverySnapshot>.Failure(
                    new Error(
                        "hive.provider.discovery.test-failure",
                        ErrorCategory.External,
                        "The test discovery provider failed during refresh."));
            }

            var now = DateTimeOffset.UtcNow;
            var observedAt = _staleFirst && callNumber == 1
                ? now.AddMinutes(-1)
                : now;
            var staleAfter = _staleFirst && callNumber == 1
                ? now.AddSeconds(-1)
                : observedAt.AddMinutes(5);

            var snapshot = new ProviderDiscoverySnapshot(
                _mismatchedResult ? ProviderId.New() : provider.Id,
                account.Id,
                _mismatchedEndpointPathCase
                    ? new Uri(
                        target.Endpoint.AbsoluteUri.Replace(
                            "/v1/",
                            "/V1/",
                            StringComparison.Ordinal))
                    : target.Endpoint,
                new ProviderOperationalMetadata(
                    ProviderAvailabilityStatus.Available,
                    ProviderHealthStatus.Unknown,
                    observedAt,
                    staleAfter),
                ProviderDiscoveryState.Supported,
                [
                    new ProviderModelMetadata(
                        target.Model!,
                        "example",
                        null,
                        ProviderAvailabilityStatus.Available,
                        ProviderHealthStatus.Unknown,
                        [
                            new CapabilityStateEntry(
                                new CapabilityKey("vision"),
                                CapabilityState.Supported)
                        ])
                ]);

            return Result<ProviderDiscoverySnapshot>.Success(snapshot);
        }
    }
}
