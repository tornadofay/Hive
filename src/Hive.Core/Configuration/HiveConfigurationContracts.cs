namespace Hive.Core;

public enum HivePersistenceBackend
{
    SqlServer,
    Embedded
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
    private readonly bool _isValid;

    public HiveBootstrapCredentialReference(SecretId id)
    {
        if (id == default)
            throw new ArgumentException(
                "A bootstrap credential reference must contain a valid secret identity.",
                nameof(id));

        Id = id;
        _isValid = true;
    }

    public SecretId Id { get; }

    internal bool IsValid => _isValid;
}

public sealed record HivePersistenceConfiguration
{
    public const string DefaultApplicationName = "Hive";
    public const string DefaultDatabaseName = "Hive";

    public HivePersistenceConfiguration(
        HivePersistenceBackend backend,
        string? serverName,
        int? port,
        string? databaseName,
        HiveSqlAuthenticationMode authenticationMode,
        string? userName,
        HiveBootstrapCredentialReference? bootstrapCredential,
        bool encrypt,
        bool trustServerCertificate,
        bool createDatabaseIfMissing,
        int commandTimeoutSeconds = 30,
        string? embeddedStoragePath = null)
    {
        if (!Enum.IsDefined(backend))
            throw new ArgumentOutOfRangeException(nameof(backend));

        if (!Enum.IsDefined(authenticationMode))
            throw new ArgumentOutOfRangeException(nameof(authenticationMode));

        if (commandTimeoutSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(commandTimeoutSeconds));

        if (backend == HivePersistenceBackend.Embedded)
        {
            if (string.IsNullOrWhiteSpace(embeddedStoragePath))
                throw new ArgumentException(
                    "Embedded storage path is required.",
                    nameof(embeddedStoragePath));

            if (port is not null)
                throw new ArgumentException(
                    "Port is only applicable to SQL Server persistence.",
                    nameof(port));

            if (!string.IsNullOrWhiteSpace(serverName))
                throw new ArgumentException(
                    "ServerName is only applicable to SQL Server persistence.",
                    nameof(serverName));

            if (!string.IsNullOrWhiteSpace(databaseName))
                throw new ArgumentException(
                    "DatabaseName is only applicable to SQL Server persistence.",
                    nameof(databaseName));

            if (authenticationMode != HiveSqlAuthenticationMode.WindowsIntegrated)
                throw new ArgumentException(
                    "SQL authentication is not applicable to Embedded persistence.",
                    nameof(authenticationMode));

            if (!string.IsNullOrWhiteSpace(userName))
                throw new ArgumentException(
                    "UserName is only applicable to SQL Server persistence.",
                    nameof(userName));

            if (bootstrapCredential is not null)
                throw new ArgumentException(
                    "A bootstrap credential is only applicable to SQL Server persistence.",
                    nameof(bootstrapCredential));

            if (encrypt)
                throw new ArgumentException(
                    "Encryption is only applicable to SQL Server persistence.",
                    nameof(encrypt));

            if (trustServerCertificate)
                throw new ArgumentException(
                    "TrustServerCertificate is only applicable to SQL Server persistence.",
                    nameof(trustServerCertificate));

            Backend = backend;
            ServerName = string.Empty;
            Port = null;
            DatabaseName = string.Empty;
            AuthenticationMode = HiveSqlAuthenticationMode.WindowsIntegrated;
            UserName = null;
            BootstrapCredential = null;
            Encrypt = false;
            TrustServerCertificate = false;
            CreateDatabaseIfMissing = createDatabaseIfMissing;
            CommandTimeoutSeconds = commandTimeoutSeconds;
            EmbeddedStoragePath = embeddedStoragePath.Trim();
            return;
        }

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
            throw new ArgumentException(
                "A SQL login name is required when SQL password authentication is selected.",
                nameof(userName));

        if (authenticationMode == HiveSqlAuthenticationMode.WindowsIntegrated &&
            !string.IsNullOrWhiteSpace(userName))
            throw new ArgumentException(
                "A SQL login name must not be supplied for Windows integrated authentication.",
                nameof(userName));

        if (bootstrapCredential is { } reference && !reference.IsValid)
            throw new ArgumentException(
                "Bootstrap credential reference must be valid when supplied.",
                nameof(bootstrapCredential));

        if (authenticationMode == HiveSqlAuthenticationMode.WindowsIntegrated &&
            bootstrapCredential is not null)
            throw new ArgumentException(
                "A bootstrap credential must not be supplied for Windows integrated authentication.",
                nameof(bootstrapCredential));

        if (!string.IsNullOrWhiteSpace(embeddedStoragePath))
            throw new ArgumentException(
                "Embedded storage path is only applicable to Embedded persistence.",
                nameof(embeddedStoragePath));

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
        EmbeddedStoragePath = null;
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

    public string? EmbeddedStoragePath { get; }

    public static HivePersistenceConfiguration Embedded(
        string storagePath,
        bool createDatabaseIfMissing = true,
        int commandTimeoutSeconds = 30) =>
        new(
            HivePersistenceBackend.Embedded,
            null,
            null,
            null,
            HiveSqlAuthenticationMode.WindowsIntegrated,
            null,
            null,
            encrypt: false,
            trustServerCertificate: false,
            createDatabaseIfMissing,
            commandTimeoutSeconds,
            storagePath);

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

        const string prefix = "Hive-";
        const int maxDatabaseNameLength = 128;
        var maximumApplicationLength = maxDatabaseNameLength - prefix.Length;

        Span<char> buffer = stackalloc char[maximumApplicationLength];
        var count = 0;

        foreach (var character in value)
        {
            if (count == maximumApplicationLength)
                break;

            buffer[count++] =
                char.IsLetterOrDigit(character) ||
                character is '-' or '_' or '.' or ' '
                    ? character
                    : '-';
        }

        var normalized = new string(buffer[..count]).Trim();

        if (string.IsNullOrWhiteSpace(normalized))
            normalized = DefaultApplicationName;

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
