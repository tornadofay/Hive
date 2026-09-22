using Microsoft.Data.SqlClient;

namespace Hive.Persistence;

public sealed class HiveDatabaseOptions
{
    private readonly SqlConnectionStringBuilder _connectionStringBuilder;

    public HiveDatabaseOptions(
        string connectionString,
        bool createDatabaseIfMissing = true,
        int commandTimeoutSeconds = 30)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("A SQL Server connection string is required.", nameof(connectionString));

        if (commandTimeoutSeconds <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(commandTimeoutSeconds),
                commandTimeoutSeconds,
                "Command timeout must be greater than zero.");

        _connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);

        if (string.IsNullOrWhiteSpace(_connectionStringBuilder.DataSource))
            throw new ArgumentException("The SQL Server data source is required.", nameof(connectionString));

        if (string.IsNullOrWhiteSpace(_connectionStringBuilder.InitialCatalog))
            throw new ArgumentException("The Hive database name is required.", nameof(connectionString));

        ConnectionString = _connectionStringBuilder.ConnectionString;
        CreateDatabaseIfMissing = createDatabaseIfMissing;
        CommandTimeoutSeconds = commandTimeoutSeconds;
    }

    public string ConnectionString { get; }

    public bool CreateDatabaseIfMissing { get; }

    public int CommandTimeoutSeconds { get; }

    public string ServerName => _connectionStringBuilder.DataSource;

    public string DatabaseName => _connectionStringBuilder.InitialCatalog;

    public static HiveDatabaseOptions LocalDevelopment(
        string databaseName = "Hive",
        int commandTimeoutSeconds = 30)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name is required.", nameof(databaseName));

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = @"(localdb)\MSSQLLocalDB",
            InitialCatalog = databaseName.Trim(),
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            ApplicationName = "Hive"
        };

        return new HiveDatabaseOptions(
            builder.ConnectionString,
            createDatabaseIfMissing: true,
            commandTimeoutSeconds);
    }


    public static HiveDatabaseOptions FromConfiguration(
        HivePersistenceConfiguration configuration,
        SecretMaterial? credential = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.Backend != HivePersistenceBackend.SqlServer)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                "Only SQL Server persistence is supported in V1.");
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = configuration.Port is null
                ? configuration.ServerName
                : $"{configuration.ServerName},{configuration.Port.Value}",
            InitialCatalog = configuration.DatabaseName,
            ApplicationName = "Hive",
            Encrypt = configuration.Encrypt,
            TrustServerCertificate = configuration.TrustServerCertificate,
            PersistSecurityInfo = false,
            ConnectTimeout = 30
        };

        switch (configuration.AuthenticationMode)
        {
            case HiveSqlAuthenticationMode.WindowsIntegrated:
                builder.IntegratedSecurity = true;
                break;

            case HiveSqlAuthenticationMode.SqlPassword:
                if (credential is null)
                {
                    throw new ArgumentException(
                        "A SQL password credential is required.",
                        nameof(credential));
                }

                builder.IntegratedSecurity = false;
                builder.UserID = configuration.UserName!;
                builder.Password = credential.Reveal();
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(configuration),
                    "SQL authentication mode is invalid.");
        }

        return new HiveDatabaseOptions(
            builder.ConnectionString,
            configuration.CreateDatabaseIfMissing,
            configuration.CommandTimeoutSeconds);
    }

    public override string ToString() =>
        $"SQL Server={ServerName}; Database={DatabaseName}; CreateIfMissing={CreateDatabaseIfMissing}; TimeoutSeconds={CommandTimeoutSeconds}";
}