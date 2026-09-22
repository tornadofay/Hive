using System.Data;
using System.Security.Cryptography;
using System.Text;
using Hive.Core;
using Microsoft.Data.SqlClient;

namespace Hive.Persistence;

public sealed class SqlDpapiSecretStore : ISecretStore
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly HiveDatabaseOptions _options;

    public SqlDpapiSecretStore(HiveDatabaseOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<Result<Secret>> CreateAsync(
        Secret secret,
        SecretMaterial material,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            "secret",
            cancellationToken,
            connection => CreateCoreAsync(
                connection,
                secret,
                material,
                accessContext,
                cancellationToken));

    public Task<Result<SecretReadResult>> GetAsync(
        SecretId id,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            "secret",
            cancellationToken,
            connection => GetCoreAsync(
                connection,
                id,
                accessContext,
                includeMaterial: true,
                cancellationToken));

    public Task<Result<Secret>> GetDescriptorAsync(
        SecretId id,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            "secret",
            cancellationToken,
            connection => GetDescriptorCoreAsync(
                connection,
                id,
                accessContext,
                cancellationToken));

    public Task<Result<Secret>> ReplaceAsync(
        SecretId id,
        SecretMaterial replacement,
        ResourceAccessContext accessContext,
        ResourceVersion expectedVersion,
        CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(
            "secret",
            cancellationToken,
            (connection, transaction) => ReplaceCoreAsync(
                connection,
                transaction,
                id,
                replacement,
                accessContext,
                expectedVersion,
                cancellationToken));

    public Task<Result> DeleteAsync(
        SecretId id,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(
            "secret",
            cancellationToken,
            (connection, transaction) => DeleteCoreAsync(
                connection,
                transaction,
                id,
                accessContext,
                cancellationToken));

    private static async Task<Result<Secret>> CreateCoreAsync(
        SqlConnection connection,
        Secret secret,
        SecretMaterial material,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(material);

        var validation = ValidateCreate(
            secret,
            material,
            accessContext);

        if (validation is not null)
            return Result<Secret>.Failure(validation);

        EnsureWindows();

        var encryptedValue = Protect(material);

        try
        {
            await using var command = CreateCommand(
                connection,
                """
                INSERT INTO [dbo].[HiveSecrets]
                (
                    [SecretId],
                    [SecretKey],
                    [DisplayName],
                    [EncryptedValue],
                    [OwnerPrincipalId],
                    [ScopeKind],
                    [ScopeIdentity],
                    [ResourceVersion],
                    [CreatedByPrincipalId],
                    [CreatedAtUtc],
                    [CorrelationId],
                    [CausationId],
                    [LifecycleStatus],
                    [LifecycleChangedAtUtc],
                    [MetadataJson]
                )
                VALUES
                (
                    @SecretId,
                    @SecretKey,
                    @DisplayName,
                    @EncryptedValue,
                    @OwnerPrincipalId,
                    @ScopeKind,
                    @ScopeIdentity,
                    @ResourceVersion,
                    @CreatedByPrincipalId,
                    @CreatedAtUtc,
                    @CorrelationId,
                    @CausationId,
                    @LifecycleStatus,
                    @LifecycleChangedAtUtc,
                    @MetadataJson
                );
                """);

            AddSecretParameters(command, secret, encryptedValue);
            _ = await command.ExecuteNonQueryAsync(cancellationToken)
                .ConfigureAwait(false);

            return Result<Secret>.Success(secret);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encryptedValue);
        }
    }

    private static async Task<Result<SecretReadResult>> GetCoreAsync(
        SqlConnection connection,
        SecretId id,
        ResourceAccessContext accessContext,
        bool includeMaterial,
        CancellationToken cancellationToken)
    {
        var row = await LoadAsync(
            connection,
            null,
            id,
            accessContext,
            cancellationToken,
            lockForUpdate: false,
            includeMaterial).ConfigureAwait(false);

        if (row.IsFailure)
            return Result<SecretReadResult>.Failure(row.Error!);

        var loaded = row.Value!;

        if (!includeMaterial)
            throw new InvalidOperationException(
                "Secret material was requested without loading the encrypted payload.");

        EnsureWindows();

        byte[] plaintext;

        try
        {
            plaintext = ProtectedData.Unprotect(
                loaded.EncryptedValue!,
                null,
                DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException)
        {
            return Result<SecretReadResult>.Failure(
                Error.Internal(
                    "hive.secret.decrypt-failed",
                    "The stored secret could not be decrypted for the current Windows user."));
        }

        try
        {
            var value = StrictUtf8.GetString(plaintext);

            return Result<SecretReadResult>.Success(
                new SecretReadResult(
                    loaded.Secret,
                    SecretMaterial.Create(value)));
        }
        catch (DecoderFallbackException)
        {
            return Result<SecretReadResult>.Failure(
                Error.Internal(
                    "hive.secret.invalid-state",
                    "The stored secret contains invalid encrypted state."));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static async Task<Result<Secret>> GetDescriptorCoreAsync(
        SqlConnection connection,
        SecretId id,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken)
    {
        var row = await LoadAsync(
            connection,
            null,
            id,
            accessContext,
            cancellationToken,
            lockForUpdate: false,
            includeMaterial: false).ConfigureAwait(false);

        return row.IsFailure
            ? Result<Secret>.Failure(row.Error!)
            : Result<Secret>.Success(row.Value!.Secret);
    }

    private static async Task<Result<Secret>> ReplaceCoreAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        SecretId id,
        SecretMaterial replacement,
        ResourceAccessContext accessContext,
        ResourceVersion expectedVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        var row = await LoadAsync(
            connection,
            transaction,
            id,
            accessContext,
            cancellationToken,
            lockForUpdate: true,
            includeMaterial: false).ConfigureAwait(false);

        if (row.IsFailure)
            return Result<Secret>.Failure(row.Error!);

        var current = row.Value!.Secret;

        if (current.Resource.Version != expectedVersion)
        {
            return Result<Secret>.Failure(
                Error(
                    "hive.secret.stale-version",
                    ErrorCategory.Concurrency,
                    "The secret changed before replacement completed."));
        }

        EnsureWindows();

        var nextVersion = current.Resource.Version.Next();
        var nextResource = new ResourceEnvelope<SecretId>(
            ResourceKind.Secret,
            current.Id,
            current.Resource.Owner,
            current.Resource.Scope,
            nextVersion,
            current.Resource.Provenance,
            current.Resource.Lifecycle,
            current.Resource.Metadata);

        var replacementBytes = Encoding.UTF8.GetBytes(
            replacement.Reveal());
        var encryptedValue = ProtectedData.Protect(
            replacementBytes,
            null,
            DataProtectionScope.CurrentUser);

        try
        {
            await using var command = CreateCommand(
                connection,
                """
                UPDATE [dbo].[HiveSecrets]
                SET [EncryptedValue] = @EncryptedValue,
                    [ResourceVersion] = @ResourceVersion
                WHERE [SecretId] = @SecretId
                  AND [ResourceVersion] = @ExpectedVersion;
                """,
                transaction);

            command.Parameters.Add(
                GuidParameter("@SecretId", id.Value));
            command.Parameters.Add(
                BinaryParameter("@EncryptedValue", encryptedValue));
            command.Parameters.Add(
                BigIntParameter("@ResourceVersion", nextVersion.Value));
            command.Parameters.Add(
                BigIntParameter("@ExpectedVersion", expectedVersion.Value));

            var affected = await command.ExecuteNonQueryAsync(cancellationToken)
                .ConfigureAwait(false);

            if (affected != 1)
            {
                return Result<Secret>.Failure(
                    Error(
                        "hive.secret.concurrent-update",
                        ErrorCategory.Concurrency,
                        "The secret changed before replacement completed."));
            }

            return Result<Secret>.Success(
                new Secret(
                    nextResource,
                    current.Key,
                    current.DisplayName));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(replacementBytes);
            CryptographicOperations.ZeroMemory(encryptedValue);
        }
    }

    private static async Task<Result> DeleteCoreAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        SecretId id,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken)
    {
        var row = await LoadAsync(
            connection,
            transaction,
            id,
            accessContext,
            cancellationToken,
            lockForUpdate: true,
            includeMaterial: false).ConfigureAwait(false);

        if (row.IsFailure)
            return Result.Failure(row.Error!);

        await using var command = CreateCommand(
            connection,
            """
            DELETE FROM [dbo].[HiveSecrets]
            WHERE [SecretId] = @SecretId;
            """,
            transaction);

        command.Parameters.Add(
            GuidParameter("@SecretId", id.Value));

        var affected = await command.ExecuteNonQueryAsync(cancellationToken)
            .ConfigureAwait(false);

        return affected == 1
            ? Result.Success()
            : Result.Failure(
                Error.NotFound(
                    "hive.secret.not-found",
                    "The secret was not found."));
    }

    private static async Task<Result<LoadedSecret>> LoadAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        SecretId id,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken,
        bool lockForUpdate,
        bool includeMaterial)
    {
        ValidateAccessContext(accessContext);

        var lockClause = lockForUpdate
            ? " WITH (UPDLOCK, HOLDLOCK)"
            : string.Empty;

        var projection = includeMaterial
            ? """
              [SecretId],
              [SecretKey],
              [DisplayName],
              [EncryptedValue],
              [OwnerPrincipalId],
              [ScopeKind],
              [ScopeIdentity],
              [ResourceVersion],
              [CreatedByPrincipalId],
              [CreatedAtUtc],
              [CorrelationId],
              [CausationId],
              [LifecycleStatus],
              [LifecycleChangedAtUtc],
              [MetadataJson]
              """
            : """
              [SecretId],
              [SecretKey],
              [DisplayName],
              CAST(NULL AS VARBINARY(MAX)) AS [EncryptedValue],
              [OwnerPrincipalId],
              [ScopeKind],
              [ScopeIdentity],
              [ResourceVersion],
              [CreatedByPrincipalId],
              [CreatedAtUtc],
              [CorrelationId],
              [CausationId],
              [LifecycleStatus],
              [LifecycleChangedAtUtc],
              [MetadataJson]
              """;

        await using var command = CreateCommand(
            connection,
            $"""
            SELECT
                {projection}
            FROM [dbo].[HiveSecrets]{lockClause}
            WHERE [SecretId] = @SecretId;
            """,
            transaction);

        command.Parameters.Add(
            GuidParameter("@SecretId", id.Value));

        await using var reader = await command.ExecuteReaderAsync(
                CommandBehavior.SingleRow,
                cancellationToken)
            .ConfigureAwait(false);

        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return Result<LoadedSecret>.Failure(
                Error.NotFound(
                    "hive.secret.not-found",
                    "The secret was not found."));
        }

        var secret = ReadSecret(reader);

        var accessError = ValidateAccess(
            secret,
            accessContext);

        if (accessError is not null)
            return Result<LoadedSecret>.Failure(accessError);

        var encryptedValue = includeMaterial
            ? reader.IsDBNull(3)
                ? throw new InvalidOperationException(
                    "Persisted secret encryption payload is missing.")
                : (byte[])reader.GetValue(3)
            : null;

        return Result<LoadedSecret>.Success(
            new LoadedSecret(
                secret,
                encryptedValue));
    }

    private static Secret ReadSecret(SqlDataReader reader)
    {
        var metadataJson = reader.GetString(14);
        var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(
            metadataJson,
            JsonOptions) ?? throw new InvalidOperationException(
                "Persisted secret metadata is invalid.");

        return new Secret(
            new ResourceEnvelope<SecretId>(
                ResourceKind.Secret,
                new SecretId(reader.GetGuid(0)),
                new PrincipalId(reader.GetGuid(4)),
                new ResourceScope(
                    (ResourceScopeKind)reader.GetInt32(5),
                    reader.IsDBNull(6) ? null : reader.GetGuid(6)),
                new ResourceVersion(reader.GetInt64(7)),
                new ResourceProvenance(
                    new PrincipalId(reader.GetGuid(8)),
                    reader.GetDateTime(9),
                    new CorrelationId(reader.GetGuid(10)),
                    reader.IsDBNull(11)
                        ? null
                        : new CausationId(reader.GetGuid(11))),
                new ResourceLifecycle(
                    (ResourceLifecycleStatus)reader.GetInt32(12),
                    reader.GetDateTime(13)),
                metadata),
            reader.GetString(1),
            reader.GetString(2));
    }

    private static Error? ValidateCreate(
        Secret secret,
        SecretMaterial material,
        ResourceAccessContext accessContext)
    {
        ValidateAccessContext(accessContext);

        if (secret.Resource.Version != ResourceVersion.Initial)
        {
            return Error.Validation(
                "hive.secret.version-invalid",
                "A new secret must start at resource version 1.");
        }

        if (secret.Resource.Owner != accessContext.PrincipalId)
            return Forbidden(
                "hive.secret.owner-forbidden",
                "The current principal does not own the secret.");

        if (!secret.Resource.Scope.Matches(accessContext))
        {
            return Forbidden(
                "hive.secret.scope-forbidden",
                "The current access context is outside the secret scope.");
        }

        return null;
    }

    private static Error? ValidateAccess(
        Secret secret,
        ResourceAccessContext accessContext)
    {
        ValidateAccessContext(accessContext);

        if (secret.Resource.Owner != accessContext.PrincipalId)
        {
            return Forbidden(
                "hive.secret.owner-forbidden",
                "The current principal does not own the secret.");
        }

        return secret.Resource.Scope.Matches(accessContext)
            ? null
            : Forbidden(
                "hive.secret.scope-forbidden",
                "The current access context is outside the secret scope.");
    }

    private static void ValidateAccessContext(
        ResourceAccessContext accessContext)
    {
        ArgumentNullException.ThrowIfNull(accessContext);

        if (accessContext.PrincipalId is null ||
            accessContext.DeploymentId is null)
        {
            throw new InvalidOperationException(
                "A deployment and principal are required for secret access.");
        }
    }

    private static byte[] Protect(SecretMaterial material)
    {
        var plaintext = Encoding.UTF8.GetBytes(material.Reveal());

        try
        {
            return ProtectedData.Protect(
                plaintext,
                null,
                DataProtectionScope.CurrentUser);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static void AddSecretParameters(
        SqlCommand command,
        Secret secret,
        byte[] encryptedValue)
    {
        command.Parameters.Add(
            GuidParameter("@SecretId", secret.Id.Value));
        command.Parameters.Add(
            StringParameter("@SecretKey", secret.Key, 100));
        command.Parameters.Add(
            StringParameter("@DisplayName", secret.DisplayName, 200));
        command.Parameters.Add(
            BinaryParameter("@EncryptedValue", encryptedValue));
        command.Parameters.Add(
            GuidParameter("@OwnerPrincipalId", secret.Resource.Owner.Value));
        command.Parameters.Add(
            IntParameter("@ScopeKind", (int)secret.Resource.Scope.Kind));
        command.Parameters.Add(
            GuidParameter("@ScopeIdentity", secret.Resource.Scope.Identity));
        command.Parameters.Add(
            BigIntParameter("@ResourceVersion", secret.Resource.Version.Value));
        command.Parameters.Add(
            GuidParameter(
                "@CreatedByPrincipalId",
                secret.Resource.Provenance.CreatedBy.Value));
        command.Parameters.Add(
            DateTimeParameter(
                "@CreatedAtUtc",
                secret.Resource.Provenance.CreatedAtUtc));
        command.Parameters.Add(
            GuidParameter(
                "@CorrelationId",
                secret.Resource.Provenance.CorrelationId.Value));
        command.Parameters.Add(
            GuidParameter(
                "@CausationId",
                secret.Resource.Provenance.CausationId?.Value));
        command.Parameters.Add(
            IntParameter(
                "@LifecycleStatus",
                (int)secret.Resource.Lifecycle.Status));
        command.Parameters.Add(
            DateTimeParameter(
                "@LifecycleChangedAtUtc",
                secret.Resource.Lifecycle.ChangedAtUtc));
        command.Parameters.Add(
            StringParameter(
                "@MetadataJson",
                JsonSerializer.Serialize(
                    secret.Resource.Metadata,
                    JsonOptions),
                -1));
    }

    private static SqlCommand CreateCommand(
        SqlConnection connection,
        string commandText,
        SqlTransaction? transaction = null) =>
        new(commandText, connection, transaction);

    private static SqlParameter GuidParameter(
        string name,
        Guid? value) =>
        new(name, SqlDbType.UniqueIdentifier)
        {
            Value = (object?)value ?? DBNull.Value
        };

    private static SqlParameter IntParameter(
        string name,
        int value) =>
        new(name, SqlDbType.Int)
        {
            Value = value
        };

    private static SqlParameter BigIntParameter(
        string name,
        long value) =>
        new(name, SqlDbType.BigInt)
        {
            Value = value
        };

    private static SqlParameter StringParameter(
        string name,
        string? value,
        int size) =>
        new(name, SqlDbType.NVarChar, size)
        {
            Value = value ?? DBNull.Value
        };

    private static SqlParameter BinaryParameter(
        string name,
        byte[] value) =>
        new(name, SqlDbType.VarBinary, -1)
        {
            Value = value
        };

    private static SqlParameter DateTimeParameter(
        string name,
        DateTimeOffset value) =>
        new(name, SqlDbType.DateTime2)
        {
            Value = value.UtcDateTime
        };

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Hive DPAPI secret storage requires Windows.");
        }
    }

    private async Task<Result<T>> ExecuteAsync<T>(
        string resourceName,
        CancellationToken cancellationToken,
        Func<SqlConnection, Task<Result<T>>> operation)
    {
        try
        {
            await using var connection = await OpenConnectionAsync(
                cancellationToken).ConfigureAwait(false);

            return await operation(connection).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SqlException exception) when (IsConstraintConflict(exception))
        {
            return Result<T>.Failure(
                Error.Conflict(
                    $"hive.{resourceName}.duplicate",
                    $"The {resourceName} identity or key already exists."));
        }
        catch (SqlException)
        {
            return Result<T>.Failure(
                Error.External(
                    $"hive.{resourceName}.sql-failure",
                    $"SQL Server operation for the {resourceName} failed."));
        }
        catch (PlatformNotSupportedException)
        {
            return Result<T>.Failure(
                Error.Unsupported(
                    "hive.secret.dpapi-windows-only",
                    "Hive DPAPI secret storage is supported only on Windows."));
        }
        catch (Exception)
        {
            return Result<T>.Failure(
                Error.Internal(
                    $"hive.{resourceName}.invalid-state",
                    $"Persisted {resourceName} state could not be read or validated."));
        }
    }

    private async Task<Result<T>> ExecuteInTransactionAsync<T>(
        string resourceName,
        CancellationToken cancellationToken,
        Func<SqlConnection, SqlTransaction, Task<Result<T>>> operation)
    {
        try
        {
            await using var connection = await OpenConnectionAsync(
                cancellationToken).ConfigureAwait(false);

            await using var transaction =
                (SqlTransaction)await connection.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken).ConfigureAwait(false);

            var result = await operation(
                connection,
                transaction).ConfigureAwait(false);

            if (result.IsSuccess)
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SqlException exception) when (IsConstraintConflict(exception))
        {
            return Result<T>.Failure(
                Error.Conflict(
                    $"hive.{resourceName}.duplicate",
                    $"The {resourceName} identity or key already exists."));
        }
        catch (SqlException)
        {
            return Result<T>.Failure(
                Error.External(
                    $"hive.{resourceName}.sql-failure",
                    $"SQL Server operation for the {resourceName} failed."));
        }
        catch (PlatformNotSupportedException)
        {
            return Result<T>.Failure(
                Error.Unsupported(
                    "hive.secret.dpapi-windows-only",
                    "Hive DPAPI secret storage is supported only on Windows."));
        }
        catch (Exception)
        {
            return Result<T>.Failure(
                Error.Internal(
                    $"hive.{resourceName}.invalid-state",
                    $"Persisted {resourceName} state could not be read or validated."));
        }
    }

    private async Task<SqlConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_options.ConnectionString);

        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static bool IsConstraintConflict(SqlException exception) =>
        exception.Number is 2601 or 2627;

    private static Error Error(
        string code,
        ErrorCategory category,
        string message) =>
        new(code, category, message);

    private static Error Forbidden(string code, string message) =>
        new(code, ErrorCategory.Forbidden, message);

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private sealed record LoadedSecret(
        Secret Secret,
        byte[]? EncryptedValue);
}
