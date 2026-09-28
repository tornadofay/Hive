using System.Collections.Concurrent;
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
    public async Task Management_DiscoveryCacheIsSharedAcrossTargetsWithSameProviderAccountAndEndpoint()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ProviderDiscoverySharedCache");
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
        var firstTarget = CreateTarget(provider.Id, account.Id, principal, tenant);
        var secondTarget = CreateTarget(provider.Id, account.Id, principal, tenant);

        var providerStore = new SqlProviderResourceStore(database.Options);

        Assert.True(
            (await providerStore.CreateProviderAsync(provider, context)).IsSuccess);
        Assert.True(
            (await providerStore.CreateProviderAccountAsync(account, context)).IsSuccess);
        Assert.True(
            (await providerStore.CreateExecutionTargetAsync(firstTarget, context)).IsSuccess);
        Assert.True(
            (await providerStore.CreateExecutionTargetAsync(secondTarget, context)).IsSuccess);

        var discovery = new RecordingDiscovery();

        using var facade = new HiveManagementFacade(
            providerStore,
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            providerCapabilityDiscovery: discovery);

        var first = await facade.GetProviderDiscoveryAsync(
            firstTarget.Id,
            context);

        Assert.True(first.IsSuccess, first.Error?.Message);

        var second = await facade.GetProviderDiscoveryAsync(
            secondTarget.Id,
            context);

        Assert.True(second.IsSuccess, second.Error?.Message);
        Assert.Equal(1, discovery.CallCount);
        Assert.Equal(
            first.Value!.Operational.ObservedAtUtc,
            second.Value!.Operational.ObservedAtUtc);
    }

    [Fact]
    public async Task Management_SecretReplacementInvalidatesDiscoveryCache()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ProviderDiscoverySecretReplacement");
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
        var secret = CreateSecret(principal, tenant, $"discovery-secret-{Guid.NewGuid():N}");
        var secretStore = new SqlDpapiSecretStore(database.Options);

        using (var originalMaterial = SecretMaterial.Create("original-discovery-secret"))
        {
            var createdSecret = await secretStore.CreateAsync(
                secret,
                originalMaterial,
                context);

            Assert.True(createdSecret.IsSuccess, createdSecret.Error?.Message);
        }

        var account = CreateAccount(provider.Id, principal, tenant)
            .WithCredentialSecret(new SecretReference(secret.Id));
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
            secretStore,
            providerCapabilityDiscovery: discovery);

        var first = await facade.GetProviderDiscoveryAsync(
            target.Id,
            context);

        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(1, discovery.CallCount);
        Assert.Equal(
            ["original-discovery-secret"],
            discovery.Credentials);

        var cached = await facade.GetProviderDiscoveryAsync(
            target.Id,
            context);

        Assert.True(cached.IsSuccess, cached.Error?.Message);
        Assert.Equal(1, discovery.CallCount);

        using var replacementMaterial = SecretMaterial.Create("replacement-discovery-secret");

        var replaced = await facade.ReplaceSecretAsync(
            secret.Id,
            replacementMaterial,
            ResourceVersion.Initial,
            context);

        Assert.True(replaced.IsSuccess, replaced.Error?.Message);
        Assert.Equal(2, replaced.Value!.Resource.Version.Value);

        var refreshed = await facade.GetProviderDiscoveryAsync(
            target.Id,
            context);

        Assert.True(refreshed.IsSuccess, refreshed.Error?.Message);
        Assert.Equal(2, discovery.CallCount);
        Assert.Equal(
            ["original-discovery-secret", "replacement-discovery-secret"],
            discovery.Credentials);
    }

    [Fact]
    public async Task Management_InFlightDiscoveryBeforeSecretReplacementDoesNotRepopulateCurrentCache()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ProviderDiscoverySecretReplacementInFlight");
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
        var secret = CreateSecret(principal, tenant, $"discovery-secret-inflight-{Guid.NewGuid():N}");
        var secretStore = new SqlDpapiSecretStore(database.Options);

        using (var originalMaterial = SecretMaterial.Create("original-inflight-secret"))
        {
            var createdSecret = await secretStore.CreateAsync(
                secret,
                originalMaterial,
                context);

            Assert.True(createdSecret.IsSuccess, createdSecret.Error?.Message);
        }

        var account = CreateAccount(provider.Id, principal, tenant)
            .WithCredentialSecret(new SecretReference(secret.Id));
        var target = CreateTarget(provider.Id, account.Id, principal, tenant);
        var providerStore = new SqlProviderResourceStore(database.Options);

        Assert.True(
            (await providerStore.CreateProviderAsync(provider, context)).IsSuccess);
        Assert.True(
            (await providerStore.CreateProviderAccountAsync(account, context)).IsSuccess);
        Assert.True(
            (await providerStore.CreateExecutionTargetAsync(target, context)).IsSuccess);

        var started = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var discovery = new RecordingDiscovery(
            started: started,
            release: release);

        using var facade = new HiveManagementFacade(
            providerStore,
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            secretStore,
            providerCapabilityDiscovery: discovery);

        var inFlight = facade.GetProviderDiscoveryAsync(
            target.Id,
            context);

        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        using var replacementMaterial = SecretMaterial.Create("replacement-inflight-secret");

        var replaced = await facade.ReplaceSecretAsync(
            secret.Id,
            replacementMaterial,
            ResourceVersion.Initial,
            context);

        Assert.True(replaced.IsSuccess, replaced.Error?.Message);

        release.TrySetResult(true);

        var first = await inFlight;
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(
            ["original-inflight-secret"],
            discovery.Credentials);

        var current = await facade.GetProviderDiscoveryAsync(
            target.Id,
            context);

        Assert.True(current.IsSuccess, current.Error?.Message);
        Assert.Equal(2, discovery.CallCount);
        Assert.Equal(
            ["original-inflight-secret", "replacement-inflight-secret"],
            discovery.Credentials);
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
    public async Task Management_ForcedRefreshDoesNotReuseOverlappingNonForcedDiscovery()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ProviderDiscoveryForcedAfterNonForced");
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

        var firstDiscoveryStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstDiscovery =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var discovery = new RecordingDiscovery(
            started: firstDiscoveryStarted,
            release: releaseFirstDiscovery);

        using var facade = new HiveManagementFacade(
            providerStore,
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            providerCapabilityDiscovery: discovery);

        var nonForced = facade.GetProviderDiscoveryAsync(
            target.Id,
            context);

        await firstDiscoveryStarted.Task;

        var forced = facade.GetProviderDiscoveryAsync(
            target.Id,
            context,
            forceRefresh: true);

        Assert.False(forced.IsCompleted);

        releaseFirstDiscovery.TrySetResult(true);

        var nonForcedResult = await nonForced;
        var forcedResult = await forced;

        Assert.True(nonForcedResult.IsSuccess, nonForcedResult.Error?.Message);
        Assert.True(forcedResult.IsSuccess, forcedResult.Error?.Message);
        Assert.Equal(2, discovery.CallCount);
        Assert.NotSame(nonForcedResult.Value, forcedResult.Value);
    }

    [Fact]
    public async Task Management_ConcurrentForcedRefreshRequestsShareOneSuccessfulRefresh()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ProviderDiscoveryForcedRefreshConcurrency");
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

        var initial = await facade.GetProviderDiscoveryAsync(
            target.Id,
            context);

        Assert.True(initial.IsSuccess, initial.Error?.Message);
        Assert.Equal(1, discovery.CallCount);

        var requests = Enumerable
            .Range(0, 8)
            .Select(_ => facade.GetProviderDiscoveryAsync(
                target.Id,
                context,
                forceRefresh: true))
            .ToArray();

        var results = await Task.WhenAll(requests);

        Assert.All(
            results,
            result => Assert.True(result.IsSuccess, result.Error?.Message));
        Assert.Equal(2, discovery.CallCount);
        Assert.NotSame(initial.Value, results[0].Value);
        Assert.All(
            results,
            result => Assert.Same(
                results[0].Value,
                result.Value));
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
    public async Task Management_StaleRefreshDoesNotUseSystemClockForEffectiveCapabilities()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ProviderDiscoveryClockBoundary");
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

        var clock = new FakeClock(DateTimeOffset.UtcNow.AddDays(1));
        var discovery = new RecordingDiscovery(
            alwaysStale: true,
            clock: clock);

        using var facade = new HiveManagementFacade(
            providerStore,
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            providerCapabilityDiscovery: discovery,
            clock: clock);

        var submission = new InputSubmission(
        [
            new InputItem(
                "stale-discovery-image.png",
                "image/png",
                new byte[] { 1, 2, 3 })
        ]);

        var prepared = await facade.PrepareInputAsync(
            submission,
            context);

        Assert.True(prepared.IsSuccess, prepared.Error?.Message);
        Assert.Empty(prepared.Value!.PreparedInputs);

        var failure = Assert.Single(prepared.Value.Failures);
        Assert.Equal(
            "hive.execution-target.selection.no-qualifying-target",
            failure.Error.Code);
        Assert.Equal(
            ErrorCategory.Unsupported,
            failure.Error.Category);
        Assert.Equal(2, discovery.CallCount);
    }

    [Fact]
    public async Task Management_InputPreparationPreservesDiscoveryFailure_WhenDiscoveryPreventsVisionRouting()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ProviderDiscoveryInputFailure");
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

        var discovery = new RecordingDiscovery(failOnCall: 1);

        using var facade = new HiveManagementFacade(
            providerStore,
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            providerCapabilityDiscovery: discovery);

        var prepared = await facade.PrepareInputAsync(
            new InputSubmission(
            [
                new InputItem(
                    "discovery-failure-image.png",
                    "image/png",
                    new byte[] { 1, 2, 3 })
            ]),
            context);

        Assert.True(prepared.IsSuccess, prepared.Error?.Message);
        Assert.Empty(prepared.Value!.PreparedInputs);
        Assert.Equal(1, discovery.CallCount);

        Assert.Contains(
            prepared.Value.Failures,
            failure =>
                failure.Error.Code == "hive.provider.discovery.test-failure" &&
                failure.Error.Category == ErrorCategory.External &&
                failure.SourceLocation == $"Provider discovery: {target.Key}");

        Assert.Contains(
            prepared.Value.Failures,
            failure =>
                failure.Error.Code == "hive.execution-target.selection.no-qualifying-target" &&
                failure.Error.Category == ErrorCategory.Unsupported);
    }

    [Fact]
    public async Task Management_InputPreparationDoesNotDiscoverExplicitlyConfiguredRequiredCapability()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ProviderDiscoveryConfiguredVision");
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
        var target = CreateTarget(provider.Id, account.Id, principal, tenant)
            .WithCapabilities(
            [
                new CapabilityStateEntry(
                    new CapabilityKey("vision"),
                    CapabilityState.Supported)
            ]);

        var providerStore = new SqlProviderResourceStore(database.Options);

        Assert.True(
            (await providerStore.CreateProviderAsync(provider, context)).IsSuccess);
        Assert.True(
            (await providerStore.CreateProviderAccountAsync(account, context)).IsSuccess);
        Assert.True(
            (await providerStore.CreateExecutionTargetAsync(target, context)).IsSuccess);

        var discovery = new RecordingDiscovery(failOnCall: 1);

        using var facade = new HiveManagementFacade(
            providerStore,
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            providerCapabilityDiscovery: discovery);

        var prepared = await facade.PrepareInputAsync(
            new InputSubmission(
            [
                new InputItem(
                    "configured-vision-image.png",
                    "image/png",
                    new byte[] { 1, 2, 3 })
            ]),
            context);

        Assert.True(prepared.IsSuccess, prepared.Error?.Message);
        Assert.Single(prepared.Value!.PreparedInputs);
        Assert.Empty(prepared.Value.Failures);
        Assert.Equal(0, discovery.CallCount);
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

        var clock = new FakeClock(
            new DateTimeOffset(
                2030,
                1,
                2,
                3,
                4,
                5,
                TimeSpan.Zero));

        var discovery = new RecordingDiscovery(
            staleFirst: true,
            clock: clock);

        using var facade = new HiveManagementFacade(
            providerStore,
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            providerCapabilityDiscovery: discovery,
            clock: clock);

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

    private static Secret CreateSecret(
        PrincipalId principal,
        TenantId tenant,
        string key)
    {
        var now = DateTimeOffset.UtcNow;

        return new Secret(
            new ResourceEnvelope<SecretId>(
                ResourceKind.Secret,
                SecretId.New(),
                principal,
                ResourceScope.Tenant(tenant),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    principal,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            key,
            "Discovery Test Secret");
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
        private readonly bool _alwaysStale;
        private readonly int? _failOnCall;
        private readonly bool _mismatchedResult;
        private readonly bool _mismatchedEndpointPathCase;
        private readonly TimeSpan? _delay;
        private readonly IClock? _clock;
        private readonly TaskCompletionSource<bool>? _started;
        private readonly TaskCompletionSource<bool>? _release;
        private readonly ConcurrentQueue<string?> _credentials = new();
        private int _callCount;

        public RecordingDiscovery(
            bool staleFirst = false,
            bool alwaysStale = false,
            int? failOnCall = null,
            bool mismatchedResult = false,
            bool mismatchedEndpointPathCase = false,
            TimeSpan? delay = null,
            IClock? clock = null,
            TaskCompletionSource<bool>? started = null,
            TaskCompletionSource<bool>? release = null)
        {
            _staleFirst = staleFirst;
            _alwaysStale = alwaysStale;
            _failOnCall = failOnCall;
            _mismatchedResult = mismatchedResult;
            _mismatchedEndpointPathCase = mismatchedEndpointPathCase;
            _delay = delay;
            _clock = clock;
            _started = started;
            _release = release;
        }

        public int CallCount =>
            Volatile.Read(ref _callCount);

        public IReadOnlyList<string?> Credentials =>
            _credentials.ToArray();

        public async Task<Result<ProviderDiscoverySnapshot>> DiscoverAsync(
            Provider provider,
            ProviderAccount account,
            ExecutionTarget target,
            SecretMaterial? credential,
            CancellationToken cancellationToken = default)
        {
            _credentials.Enqueue(credential?.Reveal());

            var callNumber = Interlocked.Increment(ref _callCount);

            _started?.TrySetResult(true);

            if (_release is not null)
            {
                await _release.Task
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

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

            var now = _clock?.UtcNow ?? DateTimeOffset.UtcNow;
            var isStale = _alwaysStale ||
                          (_staleFirst && callNumber == 1);
            var observedAt = isStale
                ? now.AddMinutes(-1)
                : now;
            var staleAfter = isStale
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
