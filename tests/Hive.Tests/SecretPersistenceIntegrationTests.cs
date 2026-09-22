using System.Text;
using Hive.Core;
using Hive.Persistence;
using Hive.Tests.TestInfrastructure;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Hive.Tests;

public sealed class SecretPersistenceIntegrationTests
{
    [Fact]
    public async Task SecretStore_CreateReadReplaceDelete_EnforcesProtectionAuthorizationAndConcurrency()
    {
        var database = new PersistenceTestDatabase("Hive_Test_SecretStore");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options)
            .MigrateAsync();

        Assert.True(
            migration.IsSuccess,
            migration.Error?.Message);

        var store = new SqlDpapiSecretStore(database.Options);
        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var context = new ResourceAccessContext(
            DeploymentId.New(),
            tenant,
            principal);

        var secret = CreateSecret(principal, tenant, $"secret-{Guid.NewGuid():N}");
        const string original = "original-secret-value-4f3ac4d5";
        const string replacement = "replacement-secret-value-b8e24f11";

        using var originalMaterial = SecretMaterial.Create(original);

        var created = await store.CreateAsync(
            secret,
            originalMaterial,
            context);

        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.Equal(ResourceVersion.Initial, created.Value!.Resource.Version);

        var duplicate = await store.CreateAsync(
            secret,
            originalMaterial,
            context);

        Assert.True(duplicate.IsFailure);
        Assert.Equal(ErrorCategory.Conflict, duplicate.Error!.Category);

        var encrypted = await ReadEncryptedValueAsync(
            database.Options,
            secret.Id);

        Assert.False(
            encrypted.SequenceEqual(
                Encoding.UTF8.GetBytes(original)));

        var loadedDescriptor = await store.GetDescriptorAsync(
            secret.Id,
            context);

        Assert.True(
            loadedDescriptor.IsSuccess,
            loadedDescriptor.Error?.Message);

        Assert.Equal(
            ResourceVersion.Initial,
            loadedDescriptor.Value!.Resource.Version);

        Assert.Contains(
            "[REDACTED]",
            loadedDescriptor.Value.ToString(),
            StringComparison.Ordinal);

        var loaded = await store.GetAsync(
            secret.Id,
            context);

        Assert.True(loaded.IsSuccess, loaded.Error?.Message);

        using (loaded.Value!.Material)
        {
            Assert.Equal(
                original,
                loaded.Value.Material.Reveal());

            Assert.Equal(
                "[REDACTED]",
                loaded.Value.Material.ToString());
        }

        var wrongOwner = await store.GetDescriptorAsync(
            secret.Id,
            new ResourceAccessContext(
                context.DeploymentId,
                tenant,
                PrincipalId.New()));

        Assert.True(wrongOwner.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, wrongOwner.Error!.Category);

        var wrongScope = await store.GetDescriptorAsync(
            secret.Id,
            new ResourceAccessContext(
                context.DeploymentId,
                TenantId.New(),
                principal));

        Assert.True(wrongScope.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, wrongScope.Error!.Category);

        using var replacementMaterial = SecretMaterial.Create(replacement);

        var replaced = await store.ReplaceAsync(
            secret.Id,
            replacementMaterial,
            context,
            ResourceVersion.Initial);

        Assert.True(replaced.IsSuccess, replaced.Error?.Message);
        Assert.Equal(2, replaced.Value!.Resource.Version.Value);

        var stale = await store.ReplaceAsync(
            secret.Id,
            replacementMaterial,
            context,
            ResourceVersion.Initial);

        Assert.True(stale.IsFailure);
        Assert.Equal(ErrorCategory.Concurrency, stale.Error!.Category);

        var loadedReplacement = await store.GetAsync(
            secret.Id,
            context);

        Assert.True(
            loadedReplacement.IsSuccess,
            loadedReplacement.Error?.Message);

        using (loadedReplacement.Value!.Material)
        {
            Assert.Equal(
                replacement,
                loadedReplacement.Value.Material.Reveal());
        }

        var corrupted = await CorruptEncryptedValueAsync(
            database.Options,
            secret.Id);

        Assert.Equal(1, corrupted);

        var malformed = await store.GetAsync(
            secret.Id,
            context);

        Assert.True(malformed.IsFailure);
        Assert.Equal(ErrorCategory.Internal, malformed.Error!.Category);

        var deleted = await store.DeleteAsync(
            secret.Id,
            context);

        Assert.True(deleted.IsSuccess, deleted.Error?.Message);

        Assert.Equal(
            0,
            await ReadSecretRowCountAsync(
                database.Options,
                secret.Id));

        var afterDelete = await store.GetDescriptorAsync(
            secret.Id,
            context);

        Assert.True(afterDelete.IsFailure);
        Assert.Equal(ErrorCategory.NotFound, afterDelete.Error!.Category);
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
            "Test Secret");
    }

    private static async Task<byte[]> ReadEncryptedValueAsync(
        HiveDatabaseOptions options,
        SecretId id)
    {
        await using var connection = new SqlConnection(
            options.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT [EncryptedValue]
            FROM [dbo].[HiveSecrets]
            WHERE [SecretId] = @SecretId;
            """;
        command.Parameters.AddWithValue(
            "@SecretId",
            id.Value);

        var result = await command.ExecuteScalarAsync();

        Assert.NotNull(result);

        return (byte[])result!;
    }

    private static async Task<int> CorruptEncryptedValueAsync(
        HiveDatabaseOptions options,
        SecretId id)
    {
        await using var connection = new SqlConnection(
            options.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE [dbo].[HiveSecrets]
            SET [EncryptedValue] = 0x00010203
            WHERE [SecretId] = @SecretId;
            """;
        command.Parameters.AddWithValue(
            "@SecretId",
            id.Value);

        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> ReadSecretRowCountAsync(
        HiveDatabaseOptions options,
        SecretId id)
    {
        await using var connection = new SqlConnection(
            options.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM [dbo].[HiveSecrets]
            WHERE [SecretId] = @SecretId;
            """;
        command.Parameters.AddWithValue(
            "@SecretId",
            id.Value);

        return Convert.ToInt32(
            await command.ExecuteScalarAsync());
    }
}
