using Hive.Core;
using Hive.Persistence;
using Microsoft.Data.SqlClient;
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
        var builder = new SqlConnectionStringBuilder(options.ConnectionString);

        Assert.True(builder.IntegratedSecurity);
        Assert.True(builder.TrustServerCertificate);
    }

    [Fact]
    public void FromConfiguration_BuildsLocalDevelopmentOptions()
    {
        var configuration = HivePersistenceConfiguration.LocalDevelopment("Hive_Config");

        var options = HiveDatabaseOptions.FromConfiguration(configuration);

        Assert.Equal(@"(localdb)\MSSQLLocalDB", options.ServerName);
        Assert.Equal("Hive_Config", options.DatabaseName);
        Assert.True(options.CreateDatabaseIfMissing);

        var builder = new SqlConnectionStringBuilder(options.ConnectionString);
        Assert.True(builder.IntegratedSecurity);
        Assert.False(builder.Encrypt);
        Assert.True(builder.TrustServerCertificate);
    }

    [Fact]
    public void FromConfiguration_RequiresCredentialForSqlPassword()
    {
        var configuration = new HivePersistenceConfiguration(
            HivePersistenceBackend.SqlServer,
            "sql.example.test",
            1433,
            "Hive",
            HiveSqlAuthenticationMode.SqlPassword,
            "hive-user",
            new HiveBootstrapCredentialReference(SecretId.New()),
            encrypt: true,
            trustServerCertificate: false,
            createDatabaseIfMissing: false);

        Assert.Throws<ArgumentException>(
            () => HiveDatabaseOptions.FromConfiguration(configuration));
    }

    [Fact]
    public void FromConfiguration_UsesSecretMaterialWithoutEmbeddingItInPublicOptionsMetadata()
    {
        var configuration = new HivePersistenceConfiguration(
            HivePersistenceBackend.SqlServer,
            "sql.example.test",
            1433,
            "Hive",
            HiveSqlAuthenticationMode.SqlPassword,
            "hive-user",
            new HiveBootstrapCredentialReference(SecretId.New()),
            encrypt: true,
            trustServerCertificate: false,
            createDatabaseIfMissing: false);

        using var material = SecretMaterial.Create("super-secret");

        var options = HiveDatabaseOptions.FromConfiguration(
            configuration,
            material);

        var builder = new SqlConnectionStringBuilder(options.ConnectionString);

        Assert.Equal("hive-user", builder.UserID);
        Assert.Equal("super-secret", builder.Password);
        Assert.True(builder.Encrypt);
        Assert.False(builder.TrustServerCertificate);
        Assert.DoesNotContain(
            "super-secret",
            options.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ConnectionStringOptions_CreateDatabaseByDefault()
    {
        var options = new HiveDatabaseOptions(
            "Server=localhost\\MSSQLSERVER01;Database=Hive;Trusted_Connection=True;");

        Assert.True(options.CreateDatabaseIfMissing);
        Assert.Equal("Hive", options.DatabaseName);
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