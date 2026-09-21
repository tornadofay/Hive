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
        RecreateDatabase();
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

            IF OBJECT_ID(N'dbo.HiveMigrationFailureProbe', N'U') IS NOT NULL
                DROP TABLE [dbo].[HiveMigrationFailureProbe];
            """;
        command.ExecuteNonQuery();
    }

    private void RecreateDatabase()
    {
        var masterBuilder = new SqlConnectionStringBuilder(Options.ConnectionString)
        {
            InitialCatalog = "master"
        };

        using (var connection = new SqlConnection(masterBuilder.ConnectionString))
        {
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

        EnsureDatabase.For.SqlDatabase(Options.ConnectionString);
    }

    private SqlConnection CreateMasterOrDatabaseConnection()
    {
        return new SqlConnection(Options.ConnectionString);
    }

}
