namespace Hive.Core;

public enum HivePersistenceBackend
{
    SqlServer
}

public enum HiveSqlAuthenticationMode
{
    WindowsIntegrated,
    SqlPassword
}

public enum HiveDatabaseState
{
    Unknown,
    DatabaseNotFound,
    SchemaNotInitialized,
    NeedsMigration,
    Current,
    FutureSchema
}

public sealed record HivePersistenceConfiguration
{
    public HivePersistenceConfiguration(
        HivePersistenceBackend backend,
        string serverName,
        int? port,
        string databaseName,
        HiveSqlAuthenticationMode authenticationMode,
        string? userName,
        SecretReference? credentialSecret,
        bool encrypt,
        bool trustServerCertificate,
        bool createDatabaseIfMissing,
        int commandTimeoutSeconds = 30)
    {
        if (!Enum.IsDefined(backend))
            throw new ArgumentOutOfRangeException(nameof(backend));

        if (!Enum.IsDefined(authenticationMode))
            throw new ArgumentOutOfRangeException(nameof(authenticationMode));

        if (string.IsNullOrWhiteSpace(serverName))
            throw new ArgumentException("SQL Server name or instance is required.", nameof(serverName));

        if (serverName.Trim().Length > 512)
            throw new ArgumentException("SQL Server name or instance cannot exceed 512 characters.", nameof(serverName));

        if (serverName.Contains(',', StringComparison.Ordinal))
            throw new ArgumentException(
                "ServerName must not contain a port separator; use Port instead.",
                nameof(serverName));

        if (port is <= 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));

        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Hive database name is required.", nameof(databaseName));

        if (databaseName.Trim().Length > 128)
            throw new ArgumentException("Hive database name cannot exceed 128 characters.", nameof(databaseName));

        if (authenticationMode == HiveSqlAuthenticationMode.SqlPassword &&
            string.IsNullOrWhiteSpace(userName))
        {
            throw new ArgumentException(
                "A SQL login name is required when SQL password authentication is selected.",
                nameof(userName));
        }

        if (authenticationMode == HiveSqlAuthenticationMode.WindowsIntegrated &&
            !string.IsNullOrWhiteSpace(userName))
        {
            throw new ArgumentException(
                "A SQL login name must not be supplied for Windows integrated authentication.",
                nameof(userName));
        }

        if (commandTimeoutSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(commandTimeoutSeconds));

        Backend = backend;
        ServerName = serverName.Trim();
        Port = port;
        DatabaseName = databaseName.Trim();
        AuthenticationMode = authenticationMode;
        UserName = string.IsNullOrWhiteSpace(userName) ? null : userName.Trim();
        CredentialSecret = credentialSecret;
        Encrypt = encrypt;
        TrustServerCertificate = trustServerCertificate;
        CreateDatabaseIfMissing = createDatabaseIfMissing;
        CommandTimeoutSeconds = commandTimeoutSeconds;
    }

    public HivePersistenceBackend Backend { get; }

    public string ServerName { get; }

    public int? Port { get; }

    public string DatabaseName { get; }

    public HiveSqlAuthenticationMode AuthenticationMode { get; }

    public string? UserName { get; }

    public SecretReference? CredentialSecret { get; }

    public bool Encrypt { get; }

    public bool TrustServerCertificate { get; }

    public bool CreateDatabaseIfMissing { get; }

    public int CommandTimeoutSeconds { get; }

    public static HivePersistenceConfiguration LocalDevelopment(
        string databaseName = "Hive") =>
        new(
            HivePersistenceBackend.SqlServer,
            @"(localdb)\MSSQLLocalDB",
            null,
            databaseName,
            HiveSqlAuthenticationMode.WindowsIntegrated,
            null,
            null,
            encrypt: false,
            trustServerCertificate: true,
            createDatabaseIfMissing: true);
}

public sealed record HivePersistenceConnectionTest(
    bool ConnectionSucceeded,
    HiveDatabaseState DatabaseState,
    int? CurrentSchemaVersion,
    int SupportedSchemaVersion,
    string Message);

public sealed record ProviderConnectionTestResult(
    string ProviderKey,
    string ExecutionTargetKey,
    string Model,
    TimeSpan Duration,
    string Message);

public interface IProviderConnectionTester
{
    Task<Result<ProviderConnectionTestResult>> TestAsync(
        Provider provider,
        ProviderAccount account,
        ExecutionTarget target,
        SecretMaterial? credential,
        CancellationToken cancellationToken = default);
}
