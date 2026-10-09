using System.Reflection;
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
    public void FromConfiguration_RejectsEmbeddedConfiguration()
    {
        var configuration = HivePersistenceConfiguration.Embedded(
            Path.Combine(
                Path.GetTempPath(),
                "Hive",
                "embedded.db"));

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => HiveDatabaseOptions.FromConfiguration(configuration));

        Assert.Equal("configuration", exception.ParamName);
        Assert.Contains(
            "only supports SQL Server persistence",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
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
    public void ConnectionString_IsNotPubliclyExposed()
    {
        var publicProperty = typeof(HiveDatabaseOptions).GetProperty(
            nameof(HiveDatabaseOptions.ConnectionString),
            BindingFlags.Instance | BindingFlags.Public);

        Assert.Null(publicProperty);

        var internalProperty = typeof(HiveDatabaseOptions).GetProperty(
            nameof(HiveDatabaseOptions.ConnectionString),
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(internalProperty);
        Assert.NotNull(internalProperty.GetMethod);
        Assert.True(internalProperty.GetMethod!.IsAssembly);
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
    public void FromConfiguration_PreservesNamedInstanceAndWindowsIntegrationWithoutAddingPort()
    {
        var configuration = new HivePersistenceConfiguration(
            HivePersistenceBackend.SqlServer,
            @"localhost\MSSQLSERVER01",
            null,
            "Hive-Hive.Example.WinForms",
            HiveSqlAuthenticationMode.WindowsIntegrated,
            null,
            null,
            encrypt: false,
            trustServerCertificate: false,
            createDatabaseIfMissing: false);

        var options = HiveDatabaseOptions.FromConfiguration(configuration);
        var builder = new SqlConnectionStringBuilder(options.ConnectionString);

        Assert.Equal(@"localhost\MSSQLSERVER01", builder.DataSource);
        Assert.Equal("Hive-Hive.Example.WinForms", builder.InitialCatalog);
        Assert.True(builder.IntegratedSecurity);
        Assert.False(builder.Encrypt);
    }

    [Fact]
    public void DescribeSqlConnectionFailureProvidesWindowsAuthenticationGuidance()
    {
        var message = HivePersistenceConnectionTester.DescribeSqlConnectionFailure(
            18456,
            "Login failed for user 'MACHINE\\user'.");

        Assert.Contains("rejected authentication", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("current Windows account", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MACHINE", message, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeSqlConnectionFailureProvidesNamedInstanceGuidance()
    {
        var message = HivePersistenceConnectionTester.DescribeSqlConnectionFailure(
            -1,
            "A network-related or instance-specific error occurred while establishing a connection.");

        Assert.Contains("named instance", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SQL Server Browser", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DescribeSqlConnectionFailureProvidesCertificateGuidance()
    {
        var message = HivePersistenceConnectionTester.DescribeSqlConnectionFailure(
            -2146893019,
            "The certificate chain was issued by an authority that is not trusted.");

        Assert.Contains("TLS certificate validation failed", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Trust server certificate", message, StringComparison.OrdinalIgnoreCase);
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