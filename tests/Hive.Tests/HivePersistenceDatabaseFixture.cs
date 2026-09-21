using DbUp;
using Hive.Core;
using Hive.Persistence;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Hive.Tests;

public sealed class HivePersistenceDatabaseFixture
{
    public HivePersistenceDatabaseFixture()
    {
        Options = new HiveDatabaseOptions(HivePersistenceTestConfiguration.ConnectionString);
        DatabaseName = Options.DatabaseName;
        EnsureDatabase.For.SqlDatabase(Options.ConnectionString);
        Console.WriteLine($"Hive persistence test database: {DatabaseName}");
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

    private SqlConnection CreateMasterOrDatabaseConnection()
    {
        return new SqlConnection(Options.ConnectionString);
    }

}

[CollectionDefinition("HivePersistence", DisableParallelization = true)]
public sealed class HivePersistenceCollection :
    ICollectionFixture<HivePersistenceDatabaseFixture>
{
}
