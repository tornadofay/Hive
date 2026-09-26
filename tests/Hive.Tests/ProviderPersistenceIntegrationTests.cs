using Hive.Core;
using Hive.Persistence;
using Hive.Tests.TestInfrastructure;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Hive.Tests;

public sealed class ProviderPersistenceIntegrationTests
{
    [Fact]
    public async Task ProviderGraph_CrudOwnershipScopeAndConcurrency_AreEnforced()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ProviderCrud");
        database.Reset();

        var migrator = new HiveDatabaseMigrator(database.Options);
        var migration = await migrator.MigrateAsync();

        Assert.True(
            migration.IsSuccess,
            migration.Error is null
                ? "Migration failed without an error."
                : migration.Error.Message);

        var store = new SqlProviderResourceStore(database.Options);
        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var context = new ResourceAccessContext(
            DeploymentId.New(),
            tenant,
            principal);

        var provider = CreateProvider(principal, tenant);
        var createdProvider = await store.CreateProviderAsync(provider, context);

        Assert.True(createdProvider.IsSuccess, createdProvider.Error?.Message);
        Assert.Equal(ResourceVersion.Initial, createdProvider.Value!.Resource.Version);

        var duplicateProvider = await store.CreateProviderAsync(provider, context);
        Assert.True(duplicateProvider.IsFailure);
        Assert.Equal(ErrorCategory.Conflict, duplicateProvider.Error!.Category);

        var account = CreateProviderAccount(
            provider.Id,
            principal,
            tenant);

        var createdAccount = await store.CreateProviderAccountAsync(account, context);
        Assert.True(createdAccount.IsSuccess, createdAccount.Error?.Message);

        var target = CreateExecutionTarget(
            provider.Id,
            account.Id,
            principal,
            tenant);

        var createdTarget = await store.CreateExecutionTargetAsync(target, context);
        Assert.True(createdTarget.IsSuccess, createdTarget.Error?.Message);

        var loadedTarget = await store.GetExecutionTargetAsync(
            target.Id,
            context);

        Assert.True(loadedTarget.IsSuccess, loadedTarget.Error?.Message);
        Assert.Equal("example-model", loadedTarget.Value!.Model);
        Assert.Equal(CapabilityState.Supported, loadedTarget.Value.Capabilities[0].State);
        Assert.Equal(CapabilityState.Unknown, loadedTarget.Value.Capabilities[1].State);

        var accounts = await store.ListProviderAccountsAsync(
            provider.Id,
            context);
        Assert.True(accounts.IsSuccess, accounts.Error?.Message);
        Assert.Single(accounts.Value!);

        var targets = await store.ListExecutionTargetsAsync(
            account.Id,
            context);
        var allTargets = await store.ListExecutionTargetsAsync(
            context);
        Assert.True(allTargets.IsSuccess, allTargets.Error?.Message);
        Assert.Single(allTargets.Value!);

        Assert.True(targets.IsSuccess, targets.Error?.Message);
        Assert.Single(targets.Value!);

        var updatedProviderInput =
            provider.WithDisplayName("Updated Provider");

        var updatedProvider = await store.UpdateProviderAsync(
            updatedProviderInput,
            context);

        Assert.True(updatedProvider.IsSuccess, updatedProvider.Error?.Message);
        Assert.Equal(2, updatedProvider.Value!.Resource.Version.Value);
        Assert.Equal("Updated Provider", updatedProvider.Value.DisplayName);

        var staleProvider = await store.UpdateProviderAsync(
            provider.WithTransportKind("stale-transport"),
            context);

        Assert.True(staleProvider.IsFailure);
        Assert.Equal(ErrorCategory.Concurrency, staleProvider.Error!.Category);

        var updatedAccount = await store.UpdateProviderAccountAsync(
            account.WithExternalAccountId("external-2"),
            context);

        Assert.True(updatedAccount.IsSuccess, updatedAccount.Error?.Message);
        Assert.Equal(2, updatedAccount.Value!.Resource.Version.Value);

        var updatedTarget = await store.UpdateExecutionTargetAsync(
            target.WithDisplayName("Updated Target"),
            context);

        Assert.True(updatedTarget.IsSuccess, updatedTarget.Error?.Message);
        Assert.Equal(2, updatedTarget.Value!.Resource.Version.Value);

        var alternateContext = new ResourceAccessContext(
            context.DeploymentId,
            tenant,
            PrincipalId.New());

        var forbiddenByOwner = await store.GetProviderAsync(
            provider.Id,
            alternateContext);

        Assert.True(forbiddenByOwner.IsFailure);
        Assert.Equal(
            ErrorCategory.Forbidden,
            forbiddenByOwner.Error!.Category);

        var forbiddenByScope = await store.GetProviderAsync(
            provider.Id,
            new ResourceAccessContext(
                context.DeploymentId,
                TenantId.New(),
                principal));

        Assert.True(forbiddenByScope.IsFailure);
        Assert.Equal(
            ErrorCategory.Forbidden,
            forbiddenByScope.Error!.Category);

        var retiredTarget = await store.DeleteExecutionTargetAsync(
            target.Id,
            context);

        Assert.True(retiredTarget.IsSuccess, retiredTarget.Error?.Message);
        Assert.Equal(
            ResourceLifecycleStatus.Retired,
            retiredTarget.Value!.Resource.Lifecycle.Status);

        var activeTargets = await store.ListExecutionTargetsAsync(
            account.Id,
            context);

        Assert.True(activeTargets.IsSuccess, activeTargets.Error?.Message);
        Assert.Empty(activeTargets.Value!);

        var retiredTargets = await store.ListExecutionTargetsAsync(
            account.Id,
            context,
            includeRetired: true);

        Assert.True(retiredTargets.IsSuccess, retiredTargets.Error?.Message);
        Assert.Single(retiredTargets.Value!);
    }

    [Fact]
    public async Task ProviderAccount_CredentialSecretReference_PersistsAndUpdates()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ProviderCredentialRef");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var store = new SqlProviderResourceStore(database.Options);
        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var context = new ResourceAccessContext(
            DeploymentId.New(),
            tenant,
            principal);

        var provider = CreateProvider(principal, tenant);
        Assert.True(
            (await store.CreateProviderAsync(provider, context)).IsSuccess);

        var firstSecret = new SecretReference(SecretId.New());
        var account = CreateProviderAccount(
            provider.Id,
            principal,
            tenant).WithCredentialSecret(firstSecret);

        var created = await store.CreateProviderAccountAsync(
            account,
            context);

        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.Equal(firstSecret, created.Value!.CredentialSecret);

        var loaded = await store.GetProviderAccountAsync(
            account.Id,
            context);

        Assert.True(loaded.IsSuccess, loaded.Error?.Message);
        Assert.Equal(firstSecret, loaded.Value!.CredentialSecret);

        var secondSecret = new SecretReference(SecretId.New());
        var updated = await store.UpdateProviderAccountAsync(
            loaded.Value.WithCredentialSecret(secondSecret),
            context);

        Assert.True(updated.IsSuccess, updated.Error?.Message);
        Assert.Equal(secondSecret, updated.Value!.CredentialSecret);

        var reloaded = await store.GetProviderAccountAsync(
            account.Id,
            context);

        Assert.True(reloaded.IsSuccess, reloaded.Error?.Message);
        Assert.Equal(secondSecret, reloaded.Value!.CredentialSecret);
    }

    [Fact]
    public async Task MalformedCapabilityState_IsRejectedWithoutReturningCorruptDomainState()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ProviderMalformed");
        database.Reset();

        var migrator = new HiveDatabaseMigrator(database.Options);
        var migration = await migrator.MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var store = new SqlProviderResourceStore(database.Options);
        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var context = new ResourceAccessContext(
            DeploymentId.New(),
            tenant,
            principal);

        var provider = CreateProvider(principal, tenant);
        Assert.True(
            (await store.CreateProviderAsync(provider, context)).IsSuccess);

        var account = CreateProviderAccount(provider.Id, principal, tenant);
        Assert.True(
            (await store.CreateProviderAccountAsync(account, context)).IsSuccess);

        var target = CreateExecutionTarget(
            provider.Id,
            account.Id,
            principal,
            tenant);

        Assert.True(
            (await store.CreateExecutionTargetAsync(target, context)).IsSuccess);

        await using var connection = new SqlConnection(
            database.Options.ConnectionString);

        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE [dbo].[HiveExecutionTargets]
            SET [CapabilitiesJson] = N'{"unexpected":true}'
            WHERE [ExecutionTargetId] = @ExecutionTargetId;
            """;
        command.Parameters.Add(
            new SqlParameter(
                "@ExecutionTargetId",
                System.Data.SqlDbType.UniqueIdentifier)
            {
                Value = target.Id.Value
            });

        Assert.Equal(1, await command.ExecuteNonQueryAsync());

        var loaded = await store.GetExecutionTargetAsync(
            target.Id,
            context);

        Assert.True(loaded.IsFailure);
        Assert.Equal(ErrorCategory.Internal, loaded.Error!.Category);
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
            "Provider",
            "openai-compatible");
    }

    private static ProviderAccount CreateProviderAccount(
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
            "Provider Account",
            "example-account");
    }

    private static ExecutionTarget CreateExecutionTarget(
        ProviderId providerId,
        ProviderAccountId providerAccountId,
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
            providerAccountId,
            $"target-{Guid.NewGuid():N}",
            "Example Target",
            new Uri("https://example.test/v1"),
            "example-model",
            null,
            [
                new CapabilityStateEntry(
                    new CapabilityKey("text.generate"),
                    CapabilityState.Supported),
                new CapabilityStateEntry(
                    new CapabilityKey("vision"),
                    CapabilityState.Unknown)
            ]);
    }
}
