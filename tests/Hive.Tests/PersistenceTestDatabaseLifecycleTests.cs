using System.Data;
using System.Globalization;
using Hive.Tests.TestInfrastructure;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Hive.Tests;

public sealed class PersistenceTestDatabaseLifecycleTests
{
    [Fact]
    public void StaleRecoveryRequiresReservedNameAgeAndMatchingOwnershipMarker()
    {
        var createdAt = DateTimeOffset.UtcNow
            .AddDays(-2)
            .ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var runId = Guid.NewGuid();
        var ownershipToken = Guid.NewGuid();
        var name = $"Hive_TestOwned_{createdAt}_{runId.ToString("N")[..8].ToUpperInvariant()}_{ownershipToken.ToString("N")[..8].ToUpperInvariant()}_LifecycleRegression";
        var marker = $"v1|{runId:N}|{ownershipToken:N}|{createdAt}";

        Assert.True(SqlTestDatabaseLifecycle.IsStrictOwnedName(name));
        Assert.True(SqlTestDatabaseLifecycle.IsStaleOwnedName(
            name,
            DateTimeOffset.UtcNow,
            TimeSpan.FromHours(24)));
        Assert.False(SqlTestDatabaseLifecycle.IsStaleOwnedName(
            name,
            DateTimeOffset.UtcNow,
            TimeSpan.FromDays(7)));
        Assert.True(SqlTestDatabaseLifecycle.OwnershipMarkerMatchesName(name, marker));

        Assert.False(SqlTestDatabaseLifecycle.OwnershipMarkerMatchesName(
            name,
            $"v1|{Guid.NewGuid():N}|{ownershipToken:N}|{createdAt}"));
        Assert.False(SqlTestDatabaseLifecycle.OwnershipMarkerMatchesName(
            name,
            $"v1|{runId:N}|{Guid.NewGuid():N}|{createdAt}"));
        Assert.False(SqlTestDatabaseLifecycle.OwnershipMarkerMatchesName(
            "Hive_Test_AgentExecution",
            marker));
        Assert.False(SqlTestDatabaseLifecycle.IsStrictOwnedName(
            "Hive_Test_AgentExecution"));
    }

    [Fact]
    public async Task OwnedDatabaseLease_UsesUniqueEmptyDatabaseAndDropsOnlyItsDatabase()
    {
        await using var first = new PersistenceTestDatabase("LifecycleRegression");
        await using var second = new PersistenceTestDatabase("LifecycleRegression");

        Assert.NotEqual(first.DatabaseName, second.DatabaseName);
        Assert.StartsWith("Hive_TestOwned_", first.DatabaseName, StringComparison.Ordinal);
        Assert.StartsWith("Hive_TestOwned_", second.DatabaseName, StringComparison.Ordinal);
        Assert.True(SqlTestDatabaseLifecycle.IsRunActive(first.RunId));

        Assert.True(await DatabaseExistsAsync(first.DatabaseName));
        Assert.True(await DatabaseExistsAsync(second.DatabaseName));

        await using (var connection = new SqlConnection(first.Options.ConnectionString))
        {
            await connection.OpenAsync();

            await using var markerCommand = connection.CreateCommand();
            markerCommand.CommandText = """
                SELECT COUNT_BIG(1)
                FROM sys.extended_properties
                WHERE [class] = 0
                  AND [name] = N'Hive.Tests.Ownership';
                """;
            Assert.Equal(1L, Convert.ToInt64(
                await markerCommand.ExecuteScalarAsync()));

            await using var userTableCommand = connection.CreateCommand();
            userTableCommand.CommandText = """
                SELECT COUNT_BIG(1)
                FROM sys.tables
                WHERE [is_ms_shipped] = 0;
                """;
            Assert.Equal(0L, Convert.ToInt64(
                await userTableCommand.ExecuteScalarAsync()));
        }

        await first.DisposeAsync();

        Assert.False(await DatabaseExistsAsync(first.DatabaseName));
        Assert.True(await DatabaseExistsAsync(second.DatabaseName));
    }

    [Fact]
    public async Task OwnedDatabaseLease_RefusesToDropDatabaseWithChangedOwnershipMarker()
    {
        await using var database = new PersistenceTestDatabase("OwnershipMismatch");
        var originalMarker = await ReadOwnershipMarkerAsync(database.Options.ConnectionString);
        Assert.NotNull(originalMarker);

        await using (var connection = new SqlConnection(database.Options.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                EXEC sys.sp_updateextendedproperty
                    @name = @PropertyName,
                    @value = @Marker;
                """;
            command.Parameters.Add(
                new SqlParameter("@PropertyName", SqlDbType.NVarChar, 128)
                {
                    Value = "Hive.Tests.Ownership"
                });
            command.Parameters.Add(
                new SqlParameter("@Marker", SqlDbType.NVarChar, 256)
                {
                    Value = "changed-marker"
                });
            await command.ExecuteNonQueryAsync();
        }

        var cleanupFailure = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await database.DisposeAsync());
        Assert.Contains("ownership marker", cleanupFailure.Message, StringComparison.Ordinal);
        Assert.True(await DatabaseExistsAsync(database.DatabaseName));

        await using (var connection = new SqlConnection(database.Options.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                EXEC sys.sp_updateextendedproperty
                    @name = @PropertyName,
                    @value = @Marker;
                """;
            command.Parameters.Add(
                new SqlParameter("@PropertyName", SqlDbType.NVarChar, 128)
                {
                    Value = "Hive.Tests.Ownership"
                });
            command.Parameters.Add(
                new SqlParameter("@Marker", SqlDbType.NVarChar, 256)
                {
                    Value = originalMarker
                });
            await command.ExecuteNonQueryAsync();
        }

        await database.DisposeAsync();
        Assert.False(await DatabaseExistsAsync(database.DatabaseName));
    }

    [Fact]
    public async Task StaleRecoveryDropsStrictlyOwnedAbandonedDatabase()
    {
        var createdAt = DateTimeOffset.UtcNow
            .AddDays(-2)
            .ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var runId = Guid.NewGuid();
        var ownershipToken = Guid.NewGuid();
        var runToken = runId.ToString("N")[..8].ToUpperInvariant();
        var databaseToken = ownershipToken.ToString("N")[..8].ToUpperInvariant();
        var databaseName = $"Hive_TestOwned_{createdAt}_{runToken}_{databaseToken}_StaleRecoveryRegression";
        var marker = $"v1|{runId:N}|{ownershipToken:N}|{createdAt}";
        var masterBuilder = new SqlConnectionStringBuilder(
            HivePersistenceTestConfiguration.ConnectionString)
        {
            InitialCatalog = "master",
            ApplicationName = "Hive.Tests stale recovery regression",
            Pooling = false
        };
        var databaseCreated = false;

        try
        {
            // A second testhost must not see this deliberately old database as
            // abandoned while this test is still setting up its ownership marker.
            await using (var runLeaseConnection = new SqlConnection(masterBuilder.ConnectionString))
            {
                await runLeaseConnection.OpenAsync();
                await using (var leaseCommand = runLeaseConnection.CreateCommand())
                {
                    leaseCommand.CommandText = """
                        DECLARE @LockResult int;
                        EXEC @LockResult = sys.sp_getapplock
                            @Resource = @Resource,
                            @LockMode = N'Shared',
                            @LockOwner = N'Session',
                            @LockTimeout = 0,
                            @DbPrincipal = N'public';
                        SELECT @LockResult;
                        """;
                    leaseCommand.Parameters.Add(
                        new SqlParameter("@Resource", SqlDbType.NVarChar, 255)
                        {
                            Value = $"Hive.Tests.Run.{runId:N}"
                        });
                    var lockResult = Convert.ToInt32(
                        await leaseCommand.ExecuteScalarAsync(),
                        CultureInfo.InvariantCulture);
                    Assert.True(lockResult >= 0, $"Could not reserve stale test-run fixture lock: {lockResult}.");
                }

                await using (var createCommand = runLeaseConnection.CreateCommand())
                {
                    createCommand.CommandText = $"CREATE DATABASE [{databaseName}];";
                    await createCommand.ExecuteNonQueryAsync();
                    databaseCreated = true;
                }

                var databaseBuilder = new SqlConnectionStringBuilder(
                    masterBuilder.ConnectionString)
                {
                    InitialCatalog = databaseName,
                    Pooling = false
                };

                await using (var databaseConnection = new SqlConnection(databaseBuilder.ConnectionString))
                {
                    await databaseConnection.OpenAsync();
                    await using var markerCommand = databaseConnection.CreateCommand();
                    markerCommand.CommandText = """
                        EXEC sys.sp_addextendedproperty
                            @name = @PropertyName,
                            @value = @Marker;
                        """;
                    markerCommand.Parameters.Add(
                        new SqlParameter("@PropertyName", SqlDbType.NVarChar, 128)
                        {
                            Value = "Hive.Tests.Ownership"
                        });
                    markerCommand.Parameters.Add(
                        new SqlParameter("@Marker", SqlDbType.NVarChar, 256)
                        {
                            Value = marker
                        });
                    await markerCommand.ExecuteNonQueryAsync();
                }
            }

            SqlTestDatabaseLifecycle.RecoverStaleOwnedDatabases();

            var stillExists = await DatabaseExistsAsync(databaseName);
            if (!stillExists)
                databaseCreated = false;

            Assert.False(
                stillExists,
                "A strictly named, sufficiently old database with a matching ownership marker and no active run lease should be recovered.");
        }
        finally
        {
            if (databaseCreated && await DatabaseExistsAsync(databaseName))
            {
                await using var connection = new SqlConnection(masterBuilder.ConnectionString);
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"""
                    IF DB_ID(@DatabaseName) IS NOT NULL
                    BEGIN
                        ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                        DROP DATABASE [{databaseName}];
                    END;
                    """;
                command.Parameters.Add(
                    new SqlParameter("@DatabaseName", SqlDbType.NVarChar, 128)
                    {
                        Value = databaseName
                    });
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    private static async Task<bool> DatabaseExistsAsync(string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(
            HivePersistenceTestConfiguration.ConnectionString)
        {
            InitialCatalog = "master",
            ApplicationName = "Hive.Tests lifecycle regression",
            Pooling = false
        };

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DB_ID(@DatabaseName);";
        command.Parameters.Add(
            new SqlParameter("@DatabaseName", SqlDbType.NVarChar, 128)
            {
                Value = databaseName
            });

        var result = await command.ExecuteScalarAsync();
        return result is not null && result is not DBNull;
    }

    private static async Task<string?> ReadOwnershipMarkerAsync(string databaseConnectionString)
    {
        await using var connection = new SqlConnection(databaseConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CONVERT(nvarchar(256), [value])
            FROM sys.extended_properties
            WHERE [class] = 0
              AND [name] = N'Hive.Tests.Ownership';
            """;

        return await command.ExecuteScalarAsync() as string;
    }
}
