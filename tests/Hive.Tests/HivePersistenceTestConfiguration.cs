namespace Hive.Tests;

internal static class HivePersistenceTestConfiguration
{
    // Change this single line when the local developer SQL Server instance differs.
    public const string ConnectionString =
        "Server=localhost\\MSSQLSERVER01;Database=Hive_Test;Trusted_Connection=True;TrustServerCertificate=True;";
}
