using DbUp;
using Hive.Persistence;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Hive.Tests;

public sealed class HivePersistenceDatabaseFixture : IDisposable
{
    public HivePersistenceDatabaseFixture()
    {
        DatabaseName = $"Hive_Test_{Guid.NewGuid():N}";
        Options = CreateOptions(DatabaseName);
        EnsureDatabase.For.SqlDatabase(Options.ConnectionString);
    }

    public string DatabaseName { get; }

    public HiveDatabaseOptions Options { get; }

    public void ResetSchema()
    {
        using var connection = CreateMasterOrDatabaseConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            IF OBJECT_ID(N'dbo.HiveMigrationJournal', N'U') IS NOT NULL
                DROP TABLE [dbo].[HiveMigrationJournal];

            IF OBJECT_ID(N'dbo.HiveSchemaVersion', N'U') IS NOT NULL
                DROP TABLE [dbo].[HiveSchemaVersion];
            """;
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        using var connection = CreateMasterConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        var escapedName = DatabaseName.Replace("]", "]]", StringComparison.Ordinal);
        var quotedLiteral = DatabaseName.Replace("'", "''", StringComparison.Ordinal);

        command.CommandText = $"""
            IF DB_ID(N'{quotedLiteral}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{escapedName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{escapedName}];
            END;
            """;
        command.ExecuteNonQuery();
    }

    private SqlConnection CreateMasterOrDatabaseConnection()
    {
        return new SqlConnection(Options.ConnectionString);
    }

    private SqlConnection CreateMasterConnection()
    {
        var builder = new SqlConnectionStringBuilder(Options.ConnectionString)
        {
            InitialCatalog = "master"
        };

        return new SqlConnection(builder.ConnectionString);
    }

    private static HiveDatabaseOptions CreateOptions(string databaseName)
    {
        var connectionString = Environment.GetEnvironmentVariable("HIVE_TEST_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
            return HiveDatabaseOptions.LocalDevelopment(databaseName);

        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = databaseName,
            ApplicationName = "Hive.Tests"
        };

        return new HiveDatabaseOptions(
            builder.ConnectionString,
            createDatabaseIfMissing: true);
    }
}

[CollectionDefinition("HivePersistence", DisableParallelization = true)]
public sealed class HivePersistenceCollection :
    ICollectionFixture<HivePersistenceDatabaseFixture>
{
}
