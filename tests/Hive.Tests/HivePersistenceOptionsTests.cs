using Hive.Core;
using Hive.Persistence;
using Xunit;

namespace Hive.Tests;

public sealed class HivePersistenceOptionsTests
{
    [Fact]
    public void LocalDevelopment_UsesExpectedSqlServerLocalDbBoundary()
    {
        var options = HiveDatabaseOptions.LocalDevelopment("Hive_Test");

        Assert.Equal(@"(localdb)\MSSQLLocalDB", options.ServerName);
        Assert.Equal("Hive_Test", options.DatabaseName);
        Assert.True(options.CreateDatabaseIfMissing);
        Assert.Contains("Integrated Security=True", options.ConnectionString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TrustServerCertificate=True", options.ConnectionString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToString_DoesNotExposeConnectionString()
    {
        var options = new HiveDatabaseOptions(
            "Server=test;Database=Hive;User ID=hive-user;Password=super-secret;",
            createDatabaseIfMissing: false);

        var text = options.ToString();

        Assert.DoesNotContain("super-secret", text, StringComparison.Ordinal);
        Assert.DoesNotContain("hive-user", text, StringComparison.Ordinal);
        Assert.Contains("Database=Hive", text, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidConnectionString_RequiresDatabaseName()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new HiveDatabaseOptions("Server=test;Integrated Security=True;"));

        Assert.Contains("database name", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvalidTimeout_IsRejected()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new HiveDatabaseOptions(
                "Server=test;Database=Hive;Integrated Security=True;",
                commandTimeoutSeconds: 0));

        Assert.Equal("commandTimeoutSeconds", exception.ParamName);
    }
}