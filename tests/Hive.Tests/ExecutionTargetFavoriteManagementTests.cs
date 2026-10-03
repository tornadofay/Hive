using Hive.Core;
using Hive.Management;
using Hive.Persistence;
using Hive.Tests.TestInfrastructure;
using Xunit;

namespace Hive.Tests;

public sealed class ExecutionTargetFavoriteManagementTests
{
    [Fact]
    public async Task Favorites_PersistInOrder_AndReloadForSamePrincipalAndScope()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ExecutionTargetFavorites");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var context = new ResourceAccessContext(
            DeploymentId.New(),
            tenant,
            principal);

        var providerStore = new SqlProviderResourceStore(database.Options);
        var provider = CreateProvider(principal, tenant);
        Assert.True(
            (await providerStore.CreateProviderAsync(provider, context)).IsSuccess);

        var account = CreateAccount(provider.Id, principal, tenant);
        Assert.True(
            (await providerStore.CreateProviderAccountAsync(account, context)).IsSuccess);

        var first = CreateTarget(provider.Id, account.Id, principal, tenant, "first");
        var second = CreateTarget(provider.Id, account.Id, principal, tenant, "second");
        Assert.True(
            (await providerStore.CreateExecutionTargetAsync(first, context)).IsSuccess);
        Assert.True(
            (await providerStore.CreateExecutionTargetAsync(second, context)).IsSuccess);

        using var facade = new HiveManagementFacade(
            providerStore,
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            executionTargetPreferences: new SqlExecutionTargetPreferenceStore(database.Options));

        var saved = await facade.ReplaceFavoriteExecutionTargetIdsAsync(
            [second.Id, first.Id],
            context);

        Assert.True(saved.IsSuccess, saved.Error?.Message);
        Assert.Equal([second.Id, first.Id], saved.Value);

        var loaded = await facade.GetFavoriteExecutionTargetIdsAsync(context);
        Assert.True(loaded.IsSuccess, loaded.Error?.Message);
        Assert.Equal([second.Id, first.Id], loaded.Value);

        var alternateContext = new ResourceAccessContext(
            context.DeploymentId,
            tenant,
            PrincipalId.New());

        var alternate = await facade.GetFavoriteExecutionTargetIdsAsync(alternateContext);
        Assert.True(alternate.IsSuccess, alternate.Error?.Message);
        Assert.Empty(alternate.Value!);

        var otherDeploymentContext = new ResourceAccessContext(
            DeploymentId.New(),
            tenant,
            principal);

        var otherDeployment = await facade.GetFavoriteExecutionTargetIdsAsync(
            otherDeploymentContext);
        Assert.True(otherDeployment.IsSuccess, otherDeployment.Error?.Message);
        Assert.Empty(otherDeployment.Value!);
    }

    [Fact]
    public async Task SaveFavorites_RejectsDuplicateAndInaccessibleTargets()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ExecutionTargetFavoriteAuthorization");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var owner = PrincipalId.New();
        var attacker = PrincipalId.New();
        var tenant = TenantId.New();
        var ownerContext = new ResourceAccessContext(
            DeploymentId.New(),
            tenant,
            owner);
        var attackerContext = new ResourceAccessContext(
            ownerContext.DeploymentId,
            tenant,
            attacker);

        var providerStore = new SqlProviderResourceStore(database.Options);
        var provider = CreateProvider(owner, tenant);
        Assert.True(
            (await providerStore.CreateProviderAsync(provider, ownerContext)).IsSuccess);

        var account = CreateAccount(provider.Id, owner, tenant);
        Assert.True(
            (await providerStore.CreateProviderAccountAsync(account, ownerContext)).IsSuccess);

        var target = CreateTarget(
            provider.Id,
            account.Id,
            owner,
            tenant,
            "owned");
        Assert.True(
            (await providerStore.CreateExecutionTargetAsync(target, ownerContext)).IsSuccess);

        using var facade = new HiveManagementFacade(
            providerStore,
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            executionTargetPreferences: new SqlExecutionTargetPreferenceStore(database.Options));

        var duplicate = await facade.ReplaceFavoriteExecutionTargetIdsAsync(
            [target.Id, target.Id],
            ownerContext);
        Assert.True(duplicate.IsFailure);
        Assert.Equal(ErrorCategory.Validation, duplicate.Error!.Category);

        var forbidden = await facade.ReplaceFavoriteExecutionTargetIdsAsync(
            [target.Id],
            attackerContext);
        Assert.True(forbidden.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, forbidden.Error!.Category);

        var otherScopeContext = new ResourceAccessContext(
            ownerContext.DeploymentId,
            TenantId.New(),
            owner);

        var otherScope = await facade.ReplaceFavoriteExecutionTargetIdsAsync(
            [target.Id],
            otherScopeContext);
        Assert.True(otherScope.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, otherScope.Error!.Category);
    }

    [Fact]
    public async Task RetiredFavorite_RemainsPersisted_AndBecomesEligibleAgainAfterReactivation()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ExecutionTargetFavoriteLifecycle");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var context = new ResourceAccessContext(
            DeploymentId.New(),
            tenant,
            principal);

        var providerStore = new SqlProviderResourceStore(database.Options);
        var provider = CreateProvider(principal, tenant);
        await providerStore.CreateProviderAsync(provider, context);

        var account = CreateAccount(provider.Id, principal, tenant);
        await providerStore.CreateProviderAccountAsync(account, context);

        var target = CreateTarget(
            provider.Id,
            account.Id,
            principal,
            tenant,
            "lifecycle");
        await providerStore.CreateExecutionTargetAsync(target, context);

        using var facade = new HiveManagementFacade(
            providerStore,
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            executionTargetPreferences: new SqlExecutionTargetPreferenceStore(database.Options));

        var deleted = await facade.DeleteExecutionTargetAsync(target.Id, context);
        Assert.True(deleted.IsSuccess, deleted.Error?.Message);

        var saved = await facade.ReplaceFavoriteExecutionTargetIdsAsync(
            [target.Id],
            context);
        Assert.True(saved.IsSuccess, saved.Error?.Message);

        var loaded = await facade.GetFavoriteExecutionTargetIdsAsync(context);
        Assert.True(loaded.IsSuccess, loaded.Error?.Message);
        Assert.Equal([target.Id], loaded.Value);

        var reactivated = await facade.ReactivateExecutionTargetAsync(
            target.Id,
            context);
        Assert.True(reactivated.IsSuccess, reactivated.Error?.Message);

        var loadedAgain = await facade.GetFavoriteExecutionTargetIdsAsync(context);
        Assert.True(loadedAgain.IsSuccess, loadedAgain.Error?.Message);
        Assert.Equal([target.Id], loadedAgain.Value);
    }

    [Fact]
    public async Task EmptyFavorites_ClearsPreferenceState()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ExecutionTargetFavoriteClear");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var context = new ResourceAccessContext(
            DeploymentId.New(),
            tenant,
            principal);

        var providerStore = new SqlProviderResourceStore(database.Options);
        var provider = CreateProvider(principal, tenant);
        await providerStore.CreateProviderAsync(provider, context);

        var account = CreateAccount(provider.Id, principal, tenant);
        await providerStore.CreateProviderAccountAsync(account, context);

        var target = CreateTarget(
            provider.Id,
            account.Id,
            principal,
            tenant,
            "clear");
        await providerStore.CreateExecutionTargetAsync(target, context);

        using var facade = new HiveManagementFacade(
            providerStore,
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            executionTargetPreferences: new SqlExecutionTargetPreferenceStore(database.Options));

        Assert.True(
            (await facade.ReplaceFavoriteExecutionTargetIdsAsync([target.Id], context)).IsSuccess);

        var cleared = await facade.ReplaceFavoriteExecutionTargetIdsAsync(
            [],
            context);
        Assert.True(cleared.IsSuccess, cleared.Error?.Message);
        Assert.Empty(cleared.Value!);

        var loaded = await facade.GetFavoriteExecutionTargetIdsAsync(context);
        Assert.True(loaded.IsSuccess, loaded.Error?.Message);
        Assert.Empty(loaded.Value!);
    }

    private static Provider CreateProvider(PrincipalId owner, TenantId tenant) =>
        new(
            new ResourceEnvelope<ProviderId>(
                ResourceKind.Provider,
                ProviderId.New(),
                owner,
                ResourceScope.Tenant(tenant),
                ResourceVersion.Initial,
                Provenance(owner),
                ResourceLifecycle.Active(Now)),
            $"provider-{Guid.NewGuid():N}",
            "Provider",
            "openai-compatible");

    private static ProviderAccount CreateAccount(
        ProviderId providerId,
        PrincipalId owner,
        TenantId tenant) =>
        new(
            new ResourceEnvelope<ProviderAccountId>(
                ResourceKind.ProviderAccount,
                ProviderAccountId.New(),
                owner,
                ResourceScope.Tenant(tenant),
                ResourceVersion.Initial,
                Provenance(owner),
                ResourceLifecycle.Active(Now)),
            providerId,
            $"account-{Guid.NewGuid():N}",
            "Account",
            "example-account");

    private static ExecutionTarget CreateTarget(
        ProviderId providerId,
        ProviderAccountId accountId,
        PrincipalId owner,
        TenantId tenant,
        string suffix) =>
        new(
            new ResourceEnvelope<ExecutionTargetId>(
                ResourceKind.ExecutionTarget,
                ExecutionTargetId.New(),
                owner,
                ResourceScope.Tenant(tenant),
                ResourceVersion.Initial,
                Provenance(owner),
                ResourceLifecycle.Active(Now)),
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

    private static ResourceProvenance Provenance(PrincipalId owner) =>
        new(owner, Now, CorrelationId.New());

    private static DateTimeOffset Now =>
        new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
}
