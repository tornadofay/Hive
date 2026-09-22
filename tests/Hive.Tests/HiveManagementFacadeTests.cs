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

        var definition = CreateAgentDefinition(context);
        var definitionResult = await facade.CreateAgentDefinitionAsync(definition, context);
        Assert.True(definitionResult.IsSuccess, definitionResult.Error?.Message);

        var loadedDefinition = await facade.GetAgentDefinitionAsync(
            definition.Id,
            context);
        Assert.True(loadedDefinition.IsSuccess, loadedDefinition.Error?.Message);
        Assert.Equal(AgentGeneration.Base, loadedDefinition.Value!.Generation);

        var updatedDefinition = await facade.UpdateAgentDefinitionAsync(
            loadedDefinition.Value.WithDisplayName("Updated Agent"),
            context);
        Assert.True(updatedDefinition.IsSuccess, updatedDefinition.Error?.Message);
        Assert.Equal(2, updatedDefinition.Value!.Resource!.Version.Value);
        Assert.Equal("Updated Agent", updatedDefinition.Value.DisplayName);

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
        ResourceAccessContext context)
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
            AgentGeneration.Base);
    }
}
