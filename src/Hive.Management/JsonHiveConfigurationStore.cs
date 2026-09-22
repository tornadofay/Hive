using System.Text.Json;
using Hive.Core;

namespace Hive.Management;

public sealed class JsonHiveConfigurationStore : IHiveConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _filePath;

    public JsonHiveConfigurationStore(string? filePath = null)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "Hive",
                "hive-settings.json")
            : Path.GetFullPath(filePath);
    }

    public async Task<Result<HivePersistenceConfiguration>> LoadPersistenceConfigurationAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return Result<HivePersistenceConfiguration>.Success(
                    HivePersistenceConfiguration.LocalDevelopment());
            }

            await using var stream = new FileStream(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            var document = await JsonSerializer.DeserializeAsync<ConfigurationDocument>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);

            if (document is null)
            {
                return Result<HivePersistenceConfiguration>.Failure(
                    Error.Validation(
                        "hive.management.configuration-empty",
                        "The Hive settings file is empty."));
            }

            if (document.ExtensionData?.ContainsKey("credentialSecretId") == true)
            {
                return Result<HivePersistenceConfiguration>.Failure(
                    Error.Validation(
                        "hive.management.legacy-bootstrap-credential-reference",
                        "The Hive settings file contains a legacy database Secret Store credential reference. Reconfigure SQL password authentication so it uses the bootstrap credential boundary."));
            }

            return Result<HivePersistenceConfiguration>.Success(
                document.ToConfiguration());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            return Result<HivePersistenceConfiguration>.Failure(
                Error.Validation(
                    "hive.management.configuration-invalid",
                    $"The Hive settings file is invalid: {exception.Message}"));
        }
        catch (Exception exception)
        {
            return Result<HivePersistenceConfiguration>.Failure(
                new Error(
                    "hive.management.configuration-read-failed",
                    ErrorCategory.External,
                    $"Hive settings could not be read: {exception.Message}"));
        }
    }

    public async Task<Result<HivePersistenceConfiguration>> SavePersistenceConfigurationAsync(
        HivePersistenceConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        try
        {
            var directory = Path.GetDirectoryName(_filePath);

            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var temporaryPath =
                _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                await using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    useAsync: true))
                {
                    await JsonSerializer.SerializeAsync(
                        stream,
                        ConfigurationDocument.From(configuration),
                        JsonOptions,
                        cancellationToken).ConfigureAwait(false);
                }

                File.Move(
                    temporaryPath,
                    _filePath,
                    overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }

            return Result<HivePersistenceConfiguration>.Success(configuration);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result<HivePersistenceConfiguration>.Failure(
                new Error(
                    "hive.management.configuration-write-failed",
                    ErrorCategory.External,
                    $"Hive settings could not be saved: {exception.Message}"));
        }
    }

    private sealed record ConfigurationDocument(
        HivePersistenceBackend Backend,
        string ServerName,
        int? Port,
        string DatabaseName,
        HiveSqlAuthenticationMode AuthenticationMode,
        string? UserName,
        Guid? BootstrapCredentialId,
        bool Encrypt,
        bool TrustServerCertificate,
        bool CreateDatabaseIfMissing,
        int CommandTimeoutSeconds)
    {
        public HivePersistenceConfiguration ToConfiguration() =>
            new(
                Backend,
                ServerName,
                Port,
                DatabaseName,
                AuthenticationMode,
                UserName,
                BootstrapCredentialId is null
                    ? null
                    : new HiveBootstrapCredentialReference(
                        new SecretId(BootstrapCredentialId.Value)),
                Encrypt,
                TrustServerCertificate,
                CreateDatabaseIfMissing,
                CommandTimeoutSeconds);

        public static ConfigurationDocument From(
            HivePersistenceConfiguration configuration) =>
            new(
                configuration.Backend,
                configuration.ServerName,
                configuration.Port,
                configuration.DatabaseName,
                configuration.AuthenticationMode,
                configuration.UserName,
                configuration.BootstrapCredential?.Id.Value,
                configuration.Encrypt,
                configuration.TrustServerCertificate,
                configuration.CreateDatabaseIfMissing,
                configuration.CommandTimeoutSeconds);

        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; init; }
    }
}
