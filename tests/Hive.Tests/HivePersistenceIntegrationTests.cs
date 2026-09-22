using DbUp;
using Hive.Core;
using Hive.Persistence;
using Hive.Tests.TestInfrastructure;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Hive.Tests;

public sealed class HivePersistenceIntegrationTests
{
    [Fact]
    public async Task CleanAndRepeatMigration_IsIdempotentAndRecordsCurrentSchema()
    {
        var database = new PersistenceTestDatabase("Hive_Test_CleanRepeat");
        database.Reset();
        var options = database.Options;

        var migrator = new HiveDatabaseMigrator(options);

        var first = await migrator.MigrateAsync(CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error is null ? "Migration failed without an error." : $"Migration failed: {first.Error.Code} [{first.Error.Category}] {first.Error.Message}");
        Assert.NotNull(first.Value);
        Assert.Equal(HiveDatabaseMigrationStatus.Applied, first.Value.Status);
        Assert.Equal(0, first.Value.PreviousSchemaVersion);
        Assert.Equal(HiveDatabaseSchema.CurrentSchemaVersion, first.Value.CurrentSchemaVersion);
        Assert.Equal(6, first.Value.AppliedMigrationCount);

        var second = await migrator.MigrateAsync(CancellationToken.None);
        Assert.True(second.IsSuccess, second.Error is null ? "Repeat migration failed without an error." : $"Repeat migration failed: {second.Error.Code} [{second.Error.Category}] {second.Error.Message}");
        Assert.NotNull(second.Value);
        Assert.Equal(HiveDatabaseMigrationStatus.AlreadyCurrent, second.Value.Status);
        Assert.Equal(HiveDatabaseSchema.CurrentSchemaVersion, second.Value.PreviousSchemaVersion);
        Assert.Equal(HiveDatabaseSchema.CurrentSchemaVersion, second.Value.CurrentSchemaVersion);
        Assert.Equal(0, second.Value.AppliedMigrationCount);

        var storedVersion = await ReadSchemaVersionAsync(options);
        Assert.Equal(HiveDatabaseSchema.CurrentSchemaVersion, storedVersion);
        Assert.True(await IndexExistsAsync(options, "PK_HiveSchemaVersion", "HiveSchemaVersion"));
        Assert.True(await IndexExistsAsync(options, "UX_HiveSchemaVersion_SchemaVersion", "HiveSchemaVersion"));
        Assert.True(await IndexExistsAsync(options, "UX_HiveProviders_ProviderKey", "HiveProviders"));
        Assert.True(await IndexExistsAsync(options, "UX_HiveProviderAccounts_Provider_Key", "HiveProviderAccounts"));
        Assert.True(await IndexExistsAsync(options, "UX_HiveExecutionTargets_ProviderAccount_Key", "HiveExecutionTargets"));
        Assert.True(await IndexExistsAsync(options, "IX_HiveProviders_OwnerScope", "HiveProviders"));
        Assert.True(await IndexExistsAsync(options, "IX_HiveProviderAccounts_OwnerScope", "HiveProviderAccounts"));
        Assert.True(await IndexExistsAsync(options, "IX_HiveExecutionTargets_OwnerScope", "HiveExecutionTargets"));
        Assert.True(await IndexExistsAsync(options, "UX_HiveSecrets_SecretKey", "HiveSecrets"));
        Assert.True(await IndexExistsAsync(options, "IX_HiveSecrets_OwnerScope", "HiveSecrets"));
        Assert.True(await IndexExistsAsync(options, "UX_HiveEventLog_StreamVersion", "HiveEventLog"));
        Assert.True(await IndexExistsAsync(options, "IX_HiveEventLog_Stream", "HiveEventLog"));
        Assert.True(await IndexExistsAsync(options, "PK_HiveEventSnapshots", "HiveEventSnapshots"));
        Assert.True(await IndexExistsAsync(options, "PK_HiveEventOutbox", "HiveEventOutbox"));
        Assert.True(await IndexExistsAsync(options, "IX_HiveEventOutbox_Created", "HiveEventOutbox"));
        Assert.True(await IndexExistsAsync(options, "IX_HiveEventOutbox_Lease", "HiveEventOutbox"));
    }

    [Fact]
    public async Task FutureSchemaVersion_IsRejectedBeforeMigration()
    {
        var database = new PersistenceTestDatabase("Hive_Test_FutureSchema");
        database.Reset();
        var options = database.Options;

        var migrator = new HiveDatabaseMigrator(options);
        var initial = await migrator.MigrateAsync(CancellationToken.None);

        Assert.True(initial.IsSuccess, initial.Error is null ? "Initial migration failed without an error." : $"Initial migration failed: {initial.Error.Code} [{initial.Error.Category}] {initial.Error.Message}");

        await SetSchemaVersionAsync(
            options,
            HiveDatabaseSchema.CurrentSchemaVersion + 1);

        var rejected = await migrator.MigrateAsync(CancellationToken.None);

        Assert.True(rejected.IsFailure);
        Assert.NotNull(rejected.Error);
        Assert.Equal("hive.persistence.future-schema", rejected.Error.Code);
        Assert.Equal(ErrorCategory.Unsupported, rejected.Error.Category);
        Assert.Equal(
            HiveDatabaseSchema.CurrentSchemaVersion + 1,
            await ReadSchemaVersionAsync(options));
    }

    [Fact]
    public async Task FailedMigration_DoesNotAdvanceSchemaVersionOrLeavePartialChanges()
    {
        var database = new PersistenceTestDatabase("Hive_Test_FailedMigration");
        database.Reset();
        var options = database.Options;

        var migrator = new HiveDatabaseMigrator(options);
        var initial = await migrator.MigrateAsync(CancellationToken.None);

        Assert.True(
            initial.IsSuccess,
            initial.Error is null
                ? "Initial migration failed without an error."
                : $"Initial migration failed: {initial.Error.Code} [{initial.Error.Category}] {initial.Error.Message}");

        var failingUpgrade = DeployChanges
            .To.SqlDatabase(options.ConnectionString)
            .WithScript(
                "999_TestFailure",
                """
                CREATE TABLE [dbo].[HiveMigrationFailureProbe]
                (
                    [Id] INT NOT NULL
                );

                THROW 51000, 'Intentional Hive migration failure for integration testing.', 1;
                """)
            .WithTransactionPerScript()
            .LogToNowhere()
            .Build();

        var result = failingUpgrade.PerformUpgrade();

        Assert.False(
            result.Successful,
            result.Error?.Message ?? "The intentional failing migration unexpectedly succeeded.");
        Assert.Equal(
            HiveDatabaseSchema.CurrentSchemaVersion,
            await ReadSchemaVersionAsync(options));
        Assert.False(await TableExistsAsync(options, "HiveMigrationFailureProbe"));
    }

    private static async Task<int> ReadSchemaVersionAsync(HiveDatabaseOptions options)
    {
        await using var connection = new SqlConnection(options.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT [SchemaVersion]
            FROM [dbo].[HiveSchemaVersion]
            WHERE [SchemaRowId] = 1;
            """;

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task SetSchemaVersionAsync(
        HiveDatabaseOptions options,
        int version)
    {
        await using var connection = new SqlConnection(options.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE [dbo].[HiveSchemaVersion]
            SET [SchemaVersion] = @SchemaVersion,
                [RecordedAtUtc] = SYSUTCDATETIME()
            WHERE [SchemaRowId] = 1;
            """;
        command.Parameters.AddWithValue("@SchemaVersion", version);

        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private static async Task<bool> IndexExistsAsync(
        HiveDatabaseOptions options,
        string indexName,
        string tableName)
    {
        await using var connection = new SqlConnection(options.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CASE
                WHEN EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [object_id] = OBJECT_ID(@TableName)
                      AND [name] = @IndexName
                )
                THEN 1
                ELSE 0
            END;
            """;
        command.Parameters.AddWithValue("@IndexName", indexName);
        command.Parameters.AddWithValue("@TableName", $"dbo.{tableName}");

        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    private static async Task<bool> TableExistsAsync(
        HiveDatabaseOptions options,
        string tableName)
    {
        await using var connection = new SqlConnection(options.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CASE
                WHEN OBJECT_ID(@TableName, N'U') IS NULL THEN 0
                ELSE 1
            END;
            """;
        command.Parameters.AddWithValue("@TableName", $"dbo.{tableName}");

        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }
}
