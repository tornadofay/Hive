using Hive.Core;
using Microsoft.Data.SqlClient;

namespace Hive.Tests;

internal static class HivePersistenceTestConfiguration
{
    public const string ConnectionString =
        "Server=localhost\\MSSQLSERVER01;Database=Hive_Test;Trusted_Connection=True;";

    public static SqlConnectionStringBuilder CreateConnectionStringBuilder()
    {
        var builder = new SqlConnectionStringBuilder(ConnectionString)
        {
            ApplicationName = "Hive.Tests"
        };

        return builder;
    }
}
