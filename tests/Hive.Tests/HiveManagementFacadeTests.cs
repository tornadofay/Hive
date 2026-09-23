using Hive.Agents;
using Hive.Core;
using Hive.Management;
using Hive.Persistence;
using Hive.Tests.TestInfrastructure;
using Xunit;

namespace Hive.Tests;

public sealed class HiveManagementFacadeTests
{
    [Fact]
    public async Task CrudFacade_PersistsProviderGraphAndAgentDefinition()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ManagementCrud");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);
        Assert.Equal(HiveDatabaseSchema.CurrentSchemaVersion, migration.Value!.CurrentSchemaVersion);

        var facade = CreateFacade(database.Options);
        var context = CreateContext();

        var provider = CreateProvider(context);
        var providerResult = await facade.CreateProviderAsync(provider, context);
        Assert.True(providerResult.IsSuccess, providerResult.Error?.Message);

        var account = CreateProviderAccount(provider.Id, context);
        var accountResult = await facade.CreateProviderAccountAsync(account, context);
        Assert.True(accountResult.IsSuccess, accountResult.Error?.Message);

        var target = CreateExecutionTarget(provider.Id, account.Id, context);
        var targetResult = await facade.CreateExecutionTargetAsync(target, context);
        Assert.True(targetResult.IsSuccess, targetResult.Error?.Message);

        var definition = CreateAgentDefinition(context, target.Id);
        var definitionResult = await facade.CreateAgentDefinitionAsync(definition, context);
        Assert.True(definitionResult.IsSuccess, definitionResult.Error?.Message);

        var loadedDefinition = await facade.GetAgentDefinitionAsync(
            definition.Id,
            context);
        Assert.True(loadedDefinition.IsSuccess, loadedDefinition.Error?.Message);
        Assert.Equal(AgentGeneration.Base, loadedDefinition.Value!.Generation);
        Assert.Equal(target.Id, loadedDefinition.Value.ConfiguredExecutionTargetId);

        var updatedDefinition = await facade.UpdateAgentDefinitionAsync(
            loadedDefinition.Value
                .WithDisplayName("Updated Agent")
                .WithConfiguredExecutionTarget(null),
            context);
        Assert.True(updatedDefinition.IsSuccess, updatedDefinition.Error?.Message);
        Assert.Equal(2, updatedDefinition.Value!.Resource!.Version.Value);
        Assert.Equal("Updated Agent", updatedDefinition.Value.DisplayName);
        Assert.Null(updatedDefinition.Value.ConfiguredExecutionTargetId);

        var reloadedDefinition = await facade.GetAgentDefinitionAsync(
            definition.Id,
            context);
        Assert.True(reloadedDefinition.IsSuccess, reloadedDefinition.Error?.Message);
        Assert.Null(reloadedDefinition.Value!.ConfiguredExecutionTargetId);

        var listedProviders = await facade.ListProvidersAsync(context);
        Assert.True(listedProviders.IsSuccess, listedProviders.Error?.Message);
        Assert.Single(listedProviders.Value!);

        var listedTargets = await facade.ListExecutionTargetsAsync(
            account.Id,
            context);
        Assert.True(listedTargets.IsSuccess, listedTargets.Error?.Message);
        Assert.Single(listedTargets.Value!);

        var listedDefinitions = await facade.ListAgentDefinitionsAsync(context);
        Assert.True(listedDefinitions.IsSuccess, listedDefinitions.Error?.Message);
        Assert.Single(listedDefinitions.Value!);

        var retired = await facade.DeleteAgentDefinitionAsync(
            definition.Id,
            context);
        Assert.True(retired.IsSuccess, retired.Error?.Message);
        Assert.Equal(
            ResourceLifecycleStatus.Retired,
            retired.Value!.Resource!.Lifecycle.Status);

        var activeDefinitions = await facade.ListAgentDefinitionsAsync(context);
        Assert.True(activeDefinitions.IsSuccess, activeDefinitions.Error?.Message);
        Assert.Empty(activeDefinitions.Value!);

        var allDefinitions = await facade.ListAgentDefinitionsAsync(
            context,
            includeRetired: true);
        Assert.True(allDefinitions.IsSuccess, allDefinitions.Error?.Message);
        Assert.Single(allDefinitions.Value!);
    }

    [Fact]
    public async Task RetiredAgentDefinitionKey_CanBeReusedByNewActiveDefinition()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ManagementAgentKeyReuse");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var facade = CreateFacade(database.Options);
        var context = CreateContext();

        var original = await facade.CreateAgentDefinitionAsync(
            CreateAgentDefinition(context, key: "reusable-agent"),
            context);

        Assert.True(original.IsSuccess, original.Error?.Message);

        var retired = await facade.DeleteAgentDefinitionAsync(
            original.Value!.Id,
            context);

        Assert.True(retired.IsSuccess, retired.Error?.Message);
        Assert.Equal(
            ResourceLifecycleStatus.Retired,
            retired.Value!.Resource!.Lifecycle.Status);

        var replacement = await facade.CreateAgentDefinitionAsync(
            CreateAgentDefinition(context, key: "reusable-agent"),
            context);

        Assert.True(replacement.IsSuccess, replacement.Error?.Message);
        Assert.NotEqual(original.Value.Id, replacement.Value!.Id);
        Assert.Equal("reusable-agent", replacement.Value.Key);
        Assert.Equal(
            ResourceLifecycleStatus.Active,
            replacement.Value.Resource!.Lifecycle.Status);

        var all = await facade.ListAgentDefinitionsAsync(
            context,
            includeRetired: true);

        Assert.True(all.IsSuccess, all.Error?.Message);
        Assert.Equal(2, all.Value!.Count);
        Assert.Contains(
            all.Value,
            definition =>
                definition.Id == original.Value.Id &&
                definition.Resource!.Lifecycle.Status == ResourceLifecycleStatus.Retired);
        Assert.Contains(
            all.Value,
            definition =>
                definition.Id == replacement.Value.Id &&
                definition.Resource!.Lifecycle.Status == ResourceLifecycleStatus.Active);
    }

    [Fact]
    public async Task RetiredResources_CanBeReactivatedOnlyAfterDependenciesAreActive()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ManagementReactivation");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var facade = CreateFacade(database.Options);
        var context = CreateContext();

        var provider = await facade.CreateProviderAsync(
            CreateProvider(context),
            context);
        Assert.True(provider.IsSuccess, provider.Error?.Message);

        var account = await facade.CreateProviderAccountAsync(
            CreateProviderAccount(provider.Value!.Id, context),
            context);
        Assert.True(account.IsSuccess, account.Error?.Message);

        var target = await facade.CreateExecutionTargetAsync(
            CreateExecutionTarget(provider.Value.Id, account.Value!.Id, context),
            context);
        Assert.True(target.IsSuccess, target.Error?.Message);

        var agent = await facade.CreateAgentDefinitionAsync(
            CreateAgentDefinition(context, target.Value!.Id, "reactivation-agent"),
            context);
        Assert.True(agent.IsSuccess, agent.Error?.Message);

        var retiredProvider = await facade.DeleteProviderAsync(
            provider.Value.Id,
            context);
        Assert.True(retiredProvider.IsSuccess, retiredProvider.Error?.Message);
        Assert.Equal(2, retiredProvider.Value!.Resource!.Version.Value);

        var retiredAccount = await facade.DeleteProviderAccountAsync(
            account.Value.Id,
            context);
        Assert.True(retiredAccount.IsSuccess, retiredAccount.Error?.Message);
        Assert.Equal(2, retiredAccount.Value!.Resource!.Version.Value);

        var retiredTarget = await facade.DeleteExecutionTargetAsync(
            target.Value.Id,
            context);
        Assert.True(retiredTarget.IsSuccess, retiredTarget.Error?.Message);
        Assert.Equal(2, retiredTarget.Value!.Resource!.Version.Value);

        var retiredAgent = await facade.DeleteAgentDefinitionAsync(
            agent.Value!.Id,
            context);
        Assert.True(retiredAgent.IsSuccess, retiredAgent.Error?.Message);
        Assert.Equal(2, retiredAgent.Value!.Resource!.Version.Value);

        var blockedAccount = await facade.ReactivateProviderAccountAsync(
            account.Value.Id,
            context);
        Assert.True(blockedAccount.IsFailure);
        Assert.Equal(
            "hive.provider-account.provider-inactive",
            blockedAccount.Error!.Code);

        var blockedTarget = await facade.ReactivateExecutionTargetAsync(
            target.Value.Id,
            context);
        Assert.True(blockedTarget.IsFailure);
        Assert.Equal(
            "hive.execution-target.provider-inactive",
            blockedTarget.Error!.Code);

        var blockedAgent = await facade.ReactivateAgentDefinitionAsync(
            agent.Value.Id,
            context);
        Assert.True(blockedAgent.IsFailure);
        Assert.Equal(
            "hive.management.agent-definition.execution-target-retired",
            blockedAgent.Error!.Code);

        var activeProvider = await facade.ReactivateProviderAsync(
            provider.Value.Id,
            context);
        Assert.True(activeProvider.IsSuccess, activeProvider.Error?.Message);
        Assert.Equal(provider.Value.Id, activeProvider.Value!.Id);
        Assert.Equal(
            ResourceLifecycleStatus.Active,
            activeProvider.Value.Resource!.Lifecycle.Status);
        Assert.Equal(3, activeProvider.Value.Resource.Version.Value);

        var activeAccount = await facade.ReactivateProviderAccountAsync(
            account.Value.Id,
            context);
        Assert.True(activeAccount.IsSuccess, activeAccount.Error?.Message);
        Assert.Equal(account.Value.Id, activeAccount.Value!.Id);
        Assert.Equal(
            ResourceLifecycleStatus.Active,
            activeAccount.Value.Resource!.Lifecycle.Status);
        Assert.Equal(3, activeAccount.Value.Resource.Version.Value);

        var activeTarget = await facade.ReactivateExecutionTargetAsync(
            target.Value.Id,
            context);
        Assert.True(activeTarget.IsSuccess, activeTarget.Error?.Message);
        Assert.Equal(target.Value.Id, activeTarget.Value!.Id);
        Assert.Equal(
            ResourceLifecycleStatus.Active,
            activeTarget.Value.Resource.Lifecycle.Status);
        Assert.Equal(3, activeTarget.Value.Resource.Version.Value);

        var activeAgent = await facade.ReactivateAgentDefinitionAsync(
            agent.Value.Id,
            context);
        Assert.True(activeAgent.IsSuccess, activeAgent.Error?.Message);
        Assert.Equal(agent.Value.Id, activeAgent.Value!.Id);
        Assert.Equal(
            ResourceLifecycleStatus.Active,
            activeAgent.Value.Resource!.Lifecycle.Status);
        Assert.Equal(3, activeAgent.Value.Resource.Version.Value);
        Assert.Equal(
            target.Value.Id,
            activeAgent.Value.ConfiguredExecutionTargetId);
    }

    [Fact]
    public async Task ReactivatingRetiredAgentDefinition_ReportsActiveKeyConflict()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ManagementReactivationKeyConflict");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var facade = CreateFacade(database.Options);
        var context = CreateContext();

        var original = await facade.CreateAgentDefinitionAsync(
            CreateAgentDefinition(context, key: "reactivation-conflict"),
            context);
        Assert.True(original.IsSuccess, original.Error?.Message);

        var retired = await facade.DeleteAgentDefinitionAsync(
            original.Value!.Id,
            context);
        Assert.True(retired.IsSuccess, retired.Error?.Message);

        var replacement = await facade.CreateAgentDefinitionAsync(
            CreateAgentDefinition(context, key: "reactivation-conflict"),
            context);
        Assert.True(replacement.IsSuccess, replacement.Error?.Message);

        var reactivated = await facade.ReactivateAgentDefinitionAsync(
            original.Value.Id,
            context);

        Assert.True(reactivated.IsFailure);
        Assert.Equal(ErrorCategory.Conflict, reactivated.Error!.Category);
        Assert.Equal("hive.agent-definition.duplicate", reactivated.Error.Code);
    }

    [Fact]
    public async Task MultipleAgentDefinitions_CanShareExecutionTarget()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ManagementSharedTarget");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var facade = CreateFacade(database.Options);
        var context = CreateContext();

        var provider = CreateProvider(context);
        var providerResult = await facade.CreateProviderAsync(provider, context);
        Assert.True(providerResult.IsSuccess, providerResult.Error?.Message);

        var account = CreateProviderAccount(provider.Id, context);
        var accountResult = await facade.CreateProviderAccountAsync(account, context);
        Assert.True(accountResult.IsSuccess, accountResult.Error?.Message);

        var target = CreateExecutionTarget(provider.Id, account.Id, context);
        var targetResult = await facade.CreateExecutionTargetAsync(target, context);
        Assert.True(targetResult.IsSuccess, targetResult.Error?.Message);

        var first = await facade.CreateAgentDefinitionAsync(
            CreateAgentDefinition(context, target.Id, "shared-target-agent-one"),
            context);
        Assert.True(first.IsSuccess, first.Error?.Message);

        var second = await facade.CreateAgentDefinitionAsync(
            CreateAgentDefinition(context, target.Id, "shared-target-agent-two"),
            context);
        Assert.True(second.IsSuccess, second.Error?.Message);

        var updatedSecond = await facade.UpdateAgentDefinitionAsync(
            second.Value!.WithDisplayName("Updated Shared Target Agent"),
            context);

        Assert.True(updatedSecond.IsSuccess, updatedSecond.Error?.Message);
        Assert.Equal(target.Id, updatedSecond.Value!.ConfiguredExecutionTargetId);
        Assert.Equal(
            "Updated Shared Target Agent",
            updatedSecond.Value.DisplayName);
    }

    [Fact]
    public async Task Facade_RejectsMissingIdentityResourceAndDefaultIdBeforePersistence()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ManagementValidation");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var facade = CreateFacade(database.Options);
        var context = CreateContext();
        var provider = CreateProvider(context);

        var missingContext = await facade.CreateProviderAsync(
            provider,
            new ResourceAccessContext());

        Assert.True(missingContext.IsFailure);
        Assert.Equal(ErrorCategory.Validation, missingContext.Error!.Category);
        Assert.Equal("hive.management.identity-required", missingContext.Error.Code);

        var missingResource = await facade.CreateAgentDefinitionAsync(
            new AgentDefinition("agent", "Agent"),
            context);

        Assert.True(missingResource.IsFailure);
        Assert.Equal(ErrorCategory.Validation, missingResource.Error!.Category);
        Assert.Equal(
            "hive.management.agent-definition.resource-required",
            missingResource.Error.Code);

        var defaultId = await facade.GetProviderAsync(
            default,
            context);

        Assert.True(defaultId.IsFailure);
        Assert.Equal(ErrorCategory.Validation, defaultId.Error!.Category);
        Assert.Equal(
            "hive.management.provider.identity-required",
            defaultId.Error.Code);
    }

    [Fact]
    public async Task Facade_RejectsOwnerScopeAndStaleUpdates()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ManagementAuthorization");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var facade = CreateFacade(database.Options);
        var context = CreateContext();
        var provider = CreateProvider(context);

        var created = await facade.CreateProviderAsync(provider, context);
        Assert.True(created.IsSuccess, created.Error?.Message);

        var ownerMismatch = await facade.GetProviderAsync(
            provider.Id,
            new ResourceAccessContext(
                context.DeploymentId,
                context.TenantId,
                PrincipalId.New()));

        Assert.True(ownerMismatch.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, ownerMismatch.Error!.Category);

        var scopeMismatch = await facade.GetProviderAsync(
            provider.Id,
            new ResourceAccessContext(
                context.DeploymentId,
                TenantId.New(),
                context.PrincipalId));

        Assert.True(scopeMismatch.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, scopeMismatch.Error!.Category);

        var current = await facade.GetProviderAsync(
            provider.Id,
            context);
        Assert.True(current.IsSuccess, current.Error?.Message);

        var firstUpdate = await facade.UpdateProviderAsync(
            current.Value!.WithDisplayName("Current"),
            context);
        Assert.True(firstUpdate.IsSuccess, firstUpdate.Error?.Message);
        Assert.Equal(2, firstUpdate.Value!.Resource.Version.Value);

        var stale = await facade.UpdateProviderAsync(
            provider.WithDisplayName("Stale"),
            context);

        Assert.True(stale.IsFailure);
        Assert.Equal(ErrorCategory.Concurrency, stale.Error!.Category);

        var retired = await facade.DeleteProviderAsync(
            provider.Id,
            context);
        Assert.True(retired.IsSuccess, retired.Error?.Message);

        var updateRetired = await facade.UpdateProviderAsync(
            retired.Value!.WithDisplayName("Should Fail"),
            context);

        Assert.True(updateRetired.IsFailure);
        Assert.Equal(ErrorCategory.Conflict, updateRetired.Error!.Category);
    }

    [Fact]
    public async Task Management_RejectsMissingUnauthorizedAndRetiredConfiguredTargets()
    {
        var database = new PersistenceTestDatabase(
            "Hive_Test_ManagementAgentTargetValidation");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(
            database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var facade = CreateFacade(database.Options);
        var context = CreateContext();
        var provider = CreateProvider(context);

        var providerCreated = await facade.CreateProviderAsync(
            provider,
            context);
        Assert.True(providerCreated.IsSuccess, providerCreated.Error?.Message);

        var account = CreateProviderAccount(provider.Id, context);
        var accountCreated = await facade.CreateProviderAccountAsync(
            account,
            context);
        Assert.True(accountCreated.IsSuccess, accountCreated.Error?.Message);

        var target = CreateExecutionTarget(
            provider.Id,
            account.Id,
            context);

        var targetCreated = await facade.CreateExecutionTargetAsync(
            target,
            context);
        Assert.True(targetCreated.IsSuccess, targetCreated.Error?.Message);

        var missingTarget = await facade.CreateAgentDefinitionAsync(
            CreateAgentDefinition(
                context,
                ExecutionTargetId.New()),
            context);

        Assert.True(missingTarget.IsFailure);
        Assert.Equal(ErrorCategory.NotFound, missingTarget.Error!.Category);
        Assert.Equal(
            "hive.execution-target.not-found",
            missingTarget.Error.Code);

        var otherContext = new ResourceAccessContext(
            context.DeploymentId,
            context.TenantId,
            PrincipalId.New());
        var otherProvider = CreateProvider(otherContext);

        var otherProviderCreated = await facade.CreateProviderAsync(
            otherProvider,
            otherContext);
        Assert.True(
            otherProviderCreated.IsSuccess,
            otherProviderCreated.Error?.Message);

        var otherAccount = CreateProviderAccount(
            otherProvider.Id,
            otherContext);
        var otherAccountCreated = await facade.CreateProviderAccountAsync(
            otherAccount,
            otherContext);
        Assert.True(
            otherAccountCreated.IsSuccess,
            otherAccountCreated.Error?.Message);

        var otherTarget = CreateExecutionTarget(
            otherProvider.Id,
            otherAccount.Id,
            otherContext);

        var otherTargetCreated = await facade.CreateExecutionTargetAsync(
            otherTarget,
            otherContext);
        Assert.True(
            otherTargetCreated.IsSuccess,
            otherTargetCreated.Error?.Message);

        var unauthorizedTarget = await facade.CreateAgentDefinitionAsync(
            CreateAgentDefinition(
                context,
                otherTarget.Id),
            context);

        Assert.True(unauthorizedTarget.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, unauthorizedTarget.Error!.Category);

        var retired = await facade.DeleteExecutionTargetAsync(
            target.Id,
            context);
        Assert.True(retired.IsSuccess, retired.Error?.Message);
        Assert.Equal(
            ResourceLifecycleStatus.Retired,
            retired.Value!.Resource!.Lifecycle.Status);

        var retiredTarget = await facade.CreateAgentDefinitionAsync(
            CreateAgentDefinition(
                context,
                target.Id),
            context);

        Assert.True(retiredTarget.IsFailure);
        Assert.Equal(ErrorCategory.Conflict, retiredTarget.Error!.Category);
        Assert.Equal(
            "hive.management.agent-definition.execution-target-retired",
            retiredTarget.Error.Code);
    }

    [Fact]
    public async Task AgentDefinitionStore_EnforcesOwnerScopeAndConcurrency()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ManagementAgentDefinitionStore");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var store = new SqlAgentDefinitionResourceStore(database.Options);
        var context = CreateContext();
        var definition = CreateAgentDefinition(context);

        var created = await store.CreateAgentDefinitionAsync(
            definition,
            context);
        Assert.True(created.IsSuccess, created.Error?.Message);

        var duplicate = await store.CreateAgentDefinitionAsync(
            definition,
            context);
        Assert.True(duplicate.IsFailure);
        Assert.Equal(ErrorCategory.Conflict, duplicate.Error!.Category);

        var otherPrincipal = await store.GetAgentDefinitionAsync(
            definition.Id,
            new ResourceAccessContext(
                context.DeploymentId,
                context.TenantId,
                PrincipalId.New()));
        Assert.True(otherPrincipal.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, otherPrincipal.Error!.Category);

        var otherTenant = await store.GetAgentDefinitionAsync(
            definition.Id,
            new ResourceAccessContext(
                context.DeploymentId,
                TenantId.New(),
                context.PrincipalId));
        Assert.True(otherTenant.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, otherTenant.Error!.Category);

        var updated = await store.UpdateAgentDefinitionAsync(
            definition.WithDisplayName("Updated Agent"),
            context);
        Assert.True(updated.IsSuccess, updated.Error?.Message);
        Assert.Equal(2, updated.Value!.Resource!.Version.Value);

        var stale = await store.UpdateAgentDefinitionAsync(
            definition.WithDisplayName("Stale"),
            context);
        Assert.True(stale.IsFailure);
        Assert.Equal(ErrorCategory.Concurrency, stale.Error!.Category);

        var retired = await store.DeleteAgentDefinitionAsync(
            definition.Id,
            context);
        Assert.True(retired.IsSuccess, retired.Error?.Message);
        Assert.Equal(
            ResourceLifecycleStatus.Retired,
            retired.Value!.Resource!.Lifecycle.Status);
    }

    private static HiveManagementFacade CreateFacade(
        HiveDatabaseOptions options) =>
        new(
            new SqlProviderResourceStore(options),
            new SqlAgentDefinitionResourceStore(options),
            new SqlWorkItemResourceStore(options));

    private static ResourceAccessContext CreateContext() =>
        new(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());

    private static Provider CreateProvider(
        ResourceAccessContext context)
    {
        var now = DateTimeOffset.UtcNow;

        return new Provider(
            new ResourceEnvelope<ProviderId>(
                ResourceKind.Provider,
                ProviderId.New(),
                context.PrincipalId!.Value,
                ResourceScope.Tenant(context.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    context.PrincipalId.Value,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            $"management-provider-{Guid.NewGuid():N}",
            "Management Provider",
            "openai-compatible");
    }

    private static ProviderAccount CreateProviderAccount(
        ProviderId providerId,
        ResourceAccessContext context)
    {
        var now = DateTimeOffset.UtcNow;

        return new ProviderAccount(
            new ResourceEnvelope<ProviderAccountId>(
                ResourceKind.ProviderAccount,
                ProviderAccountId.New(),
                context.PrincipalId!.Value,
                ResourceScope.Tenant(context.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    context.PrincipalId.Value,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            providerId,
            $"management-account-{Guid.NewGuid():N}",
            "Management Account");
    }

    private static ExecutionTarget CreateExecutionTarget(
        ProviderId providerId,
        ProviderAccountId accountId,
        ResourceAccessContext context)
    {
        var now = DateTimeOffset.UtcNow;

        return new ExecutionTarget(
            new ResourceEnvelope<ExecutionTargetId>(
                ResourceKind.ExecutionTarget,
                ExecutionTargetId.New(),
                context.PrincipalId!.Value,
                ResourceScope.Tenant(context.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    context.PrincipalId.Value,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            providerId,
            accountId,
            $"management-target-{Guid.NewGuid():N}",
            "Management Target",
            new Uri("https://example.test/v1"),
            "example-model",
            null,
            [
                new CapabilityStateEntry(
                    new CapabilityKey("text.generate"),
                    CapabilityState.Supported)
            ]);
    }

    private static AgentDefinition CreateAgentDefinition(
        ResourceAccessContext context,
        ExecutionTargetId? configuredExecutionTargetId = null,
        string key = "base-agent")
    {
        var now = DateTimeOffset.UtcNow;

        return new AgentDefinition(
            new ResourceEnvelope<AgentDefinitionId>(
                ResourceKind.AgentDefinition,
                AgentDefinitionId.New(),
                context.PrincipalId!.Value,
                ResourceScope.Tenant(context.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    context.PrincipalId.Value,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            "base-agent",
            "Base Agent",
            AgentGeneration.Base,
            configuredExecutionTargetId);
    }
}
