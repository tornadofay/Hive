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

public readonly record struct HiveBootstrapCredentialReference
{
    public HiveBootstrapCredentialReference(SecretId id)
    {
        if (id.Value == Guid.Empty)
            throw new ArgumentException(
                "A bootstrap credential reference must contain a valid secret identity.",
                nameof(id));

        Id = id;
    }

    public SecretId Id { get; }
}

public sealed record HivePersistenceConfiguration
{
    public const string DefaultApplicationName = "Hive";

    public HivePersistenceConfiguration(
        HivePersistenceBackend backend,
        string serverName,
        int? port,
        string databaseName,
        HiveSqlAuthenticationMode authenticationMode,
        string? userName,
        HiveBootstrapCredentialReference? bootstrapCredential,
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
        BootstrapCredential = bootstrapCredential;
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

    public HiveBootstrapCredentialReference? BootstrapCredential { get; }

    public bool Encrypt { get; }

    public bool TrustServerCertificate { get; }

    public bool CreateDatabaseIfMissing { get; }

    public int CommandTimeoutSeconds { get; }

    public static HivePersistenceConfiguration LocalDevelopment(
        string databaseName = DefaultDatabaseName) =>
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

    public static HivePersistenceConfiguration LocalDevelopmentForApplication(
        string? applicationName) =>
        LocalDevelopment(BuildDatabaseName(applicationName));

    public static string BuildDatabaseName(string? applicationName)
    {
        var value = string.IsNullOrWhiteSpace(applicationName)
            ? DefaultApplicationName
            : applicationName.Trim();

        Span<char> buffer = stackalloc char[value.Length];
        var count = 0;

        foreach (var character in value)
        {
            buffer[count++] =
                char.IsLetterOrDigit(character) ||
                character is '-' or '_' or '.' or ' '
                    ? character
                    : '-';
        }

        var normalized = new string(buffer[..count]).Trim();

        if (string.IsNullOrWhiteSpace(normalized))
            normalized = DefaultApplicationName;

        const string prefix = "Hive-";
        const int maxDatabaseNameLength = 128;
        var maximumApplicationLength = maxDatabaseNameLength - prefix.Length;

        if (normalized.Length > maximumApplicationLength)
            normalized = normalized[..maximumApplicationLength].TrimEnd();

        return prefix + normalized;
    }
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
