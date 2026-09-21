using DbUp;
using DbUp.Engine.Output;
using Hive.Persistence;
using Microsoft.Data.SqlClient;

namespace Hive.Tests.TestInfrastructure;

internal sealed class PersistenceTestDatabase
{
    public PersistenceTestDatabase(string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name is required.", nameof(databaseName));

        var builder = new SqlConnectionStringBuilder(
            HivePersistenceTestConfiguration.ConnectionString)
        {
            InitialCatalog = databaseName,
            ApplicationName = "Hive.Tests"
        };

        Options = new HiveDatabaseOptions(
            builder.ConnectionString,
            createDatabaseIfMissing: true);

        EnsureDatabase.For.SqlDatabase(
            Options.ConnectionString,
            new NoOpUpgradeLog());
    }

    public HiveDatabaseOptions Options { get; }

    public void Reset()
    {
        using var connection = new SqlConnection(Options.ConnectionString);
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
}
