using System.Data;
using System.Text.Json;
using Hive.Core;
using Microsoft.Data.SqlClient;

namespace Hive.Persistence;

internal abstract class SqlResourceStoreBase
{
    protected const string ScopeAccessPredicate = """
        (
            [ScopeKind] = @GlobalScope
            OR ([ScopeKind] = @TenantScope AND @TenantId IS NOT NULL AND [ScopeIdentity] = @TenantId)
            OR ([ScopeKind] = @UserScope AND @UserId IS NOT NULL AND [ScopeIdentity] = @UserId)
            OR ([ScopeKind] = @WorkspaceScope AND @WorkspaceId IS NOT NULL AND [ScopeIdentity] = @WorkspaceId)
            OR ([ScopeKind] = @AgentScope AND @AgentId IS NOT NULL AND [ScopeIdentity] = @AgentId)
            OR ([ScopeKind] = @RuntimeScope AND @RuntimeId IS NOT NULL AND [ScopeIdentity] = @RuntimeId)
            OR ([ScopeKind] = @ExecutionScope AND @ExecutionId IS NOT NULL AND [ScopeIdentity] = @ExecutionId)
        )
        """;

    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);

    protected readonly HiveDatabaseOptions _options;

    protected SqlResourceStoreBase(HiveDatabaseOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    protected static IReadOnlyDictionary<string, string> DeserializeMetadata(
        string json)
    {
        var value = JsonSerializer.Deserialize<Dictionary<string, string>>(
            json,
            JsonOptions);

        return value is null
            ? throw new InvalidOperationException(
                "Persisted metadata JSON is invalid.")
            : new Dictionary<string, string>(
                value,
                StringComparer.Ordinal);
    }


    protected static IReadOnlyList<CapabilityStateEntry> DeserializeCapabilities(
        string json)
    {
        var items = JsonSerializer.Deserialize<List<CapabilityPersistenceItem>>(
            json,
            JsonOptions);

        if (items is null)
        {
            throw new InvalidOperationException(
                "Persisted capability JSON is invalid.");
        }

        var capabilities = new List<CapabilityStateEntry>(items.Count);

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Key))
            {
                throw new InvalidOperationException(
                    "Persisted capability key is invalid.");
            }

            if (!Enum.IsDefined(item.State))
            {
                throw new InvalidOperationException(
                    "Persisted capability state is invalid.");
            }

            capabilities.Add(
                new CapabilityStateEntry(
                    new CapabilityKey(item.Key),
                    item.State));
        }

        return capabilities;
    }


    protected static string SerializeMetadata(
        IReadOnlyDictionary<string, string> metadata) =>
        JsonSerializer.Serialize(metadata, JsonOptions);


    protected static string SerializeCapabilities(
        IReadOnlyList<CapabilityStateEntry> capabilities) =>
        JsonSerializer.Serialize(
            capabilities.Select(
                capability => new CapabilityPersistenceItem(
                    capability.Capability.Value,
                    capability.State)),
            JsonOptions);


    protected static Error? ValidateCreate<TIdentity>(
        ResourceEnvelope<TIdentity> resource,
        ResourceKind expectedKind,
        ResourceAccessContext accessContext,
        string resourceName)
        where TIdentity : struct
    {
        ValidateAccessContext(accessContext);

        if (resource.Kind != expectedKind)
        {
            return Error.Validation(
                "hive.resource.kind-invalid",
                $"The {resourceName} resource kind is invalid.");
        }

        if (resource.Version != ResourceVersion.Initial)
        {
            return Error.Validation(
                "hive.resource.version-invalid",
                $"A new {resourceName} must start at resource version 1.");
        }

        if (resource.Lifecycle.Status != ResourceLifecycleStatus.Active)
        {
            return Error.Validation(
                "hive.resource.lifecycle-invalid",
                $"A new {resourceName} must start in Active lifecycle state.");
        }

        return ValidateAccess(
            resource,
            accessContext,
            resourceName);
    }


    protected static Error? ValidateUpdate<TIdentity>(
        ResourceEnvelope<TIdentity> resource,
        ResourceKind expectedKind,
        ResourceAccessContext accessContext,
        string resourceName)
        where TIdentity : struct
    {
        ValidateAccessContext(accessContext);

        if (resource.Kind != expectedKind)
        {
            return Error.Validation(
                "hive.resource.kind-invalid",
                $"The {resourceName} resource kind is invalid.");
        }

        if (resource.Version.Value <= 0)
        {
            return Error.Validation(
                "hive.resource.version-invalid",
                $"The {resourceName} resource version is invalid.");
        }

        return ValidateAccess(
            resource,
            accessContext,
            resourceName);
    }


    protected static Error? ValidateAccess<TIdentity>(
        ResourceEnvelope<TIdentity> resource,
        ResourceAccessContext accessContext,
        string resourceName)
        where TIdentity : struct
    {
        if (resource.Owner != accessContext.PrincipalId)
        {
            return new Error(
                "hive.resource.owner-forbidden",
                ErrorCategory.Forbidden,
                $"The current principal does not own the {resourceName}.");
        }

        return resource.Scope.Matches(accessContext)
            ? null
            : new Error(
                "hive.resource.scope-forbidden",
                ErrorCategory.Forbidden,
                $"The current access context is outside the {resourceName} scope.");
    }


    protected static void ValidateAccessContext(
        ResourceAccessContext accessContext)
    {
        ArgumentNullException.ThrowIfNull(accessContext);

        if (accessContext.PrincipalId is null ||
            accessContext.DeploymentId is null)
        {
            throw new InvalidOperationException(
                "A deployment and principal are required for provider resource access.");
        }
    }


    protected async Task<Result<T>> ExecuteAsync<T>(
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
                Conflict(
                    $"hive.{resourceName.Replace(' ', '-')}.duplicate",
                    $"The {resourceName} identity or key already exists."));
        }
        catch (SqlException exception)
        {
            return Result<T>.Failure(
                ToSqlError(resourceName, exception));
        }
        catch (ConcurrencyException)
        {
            return Result<T>.Failure(
                Concurrency(
                    $"hive.{resourceName.Replace(' ', '-')}.concurrency",
                    $"The {resourceName} changed before the operation completed."));
        }
        catch (Exception exception)
        {
            return Result<T>.Failure(
                ToInvalidStateError(resourceName, exception));
        }
    }


    protected async Task<Result<T>> ExecuteInTransactionAsync<T>(
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
                Conflict(
                    $"hive.{resourceName.Replace(' ', '-')}.duplicate",
                    $"The {resourceName} identity or key already exists."));
        }
        catch (SqlException exception)
        {
            return Result<T>.Failure(
                ToSqlError(resourceName, exception));
        }
        catch (ConcurrencyException)
        {
            return Result<T>.Failure(
                Concurrency(
                    $"hive.{resourceName.Replace(' ', '-')}.concurrency",
                    $"The {resourceName} changed before the operation completed."));
        }
        catch (Exception exception)
        {
            return Result<T>.Failure(
                ToInvalidStateError(resourceName, exception));
        }
    }


    protected async Task<SqlConnection> OpenConnectionAsync(
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


    protected SqlCommand CreateCommand(
        SqlConnection connection,
        string commandText,
        SqlTransaction? transaction = null) =>
        new(commandText, connection, transaction)
        {
            CommandTimeout = _options.CommandTimeoutSeconds
        };


    protected static void AddResourceParameters<TIdentity>(
        SqlCommand command,
        ResourceEnvelope<TIdentity> resource)
        where TIdentity : struct
    {
        command.Parameters.Add(
            GuidParameter(
                "@OwnerPrincipalId",
                resource.Owner.Value));
        command.Parameters.Add(
            IntParameter(
                "@ScopeKind",
                (int)resource.Scope.Kind));
        command.Parameters.Add(
            GuidParameter(
                "@ScopeIdentity",
                resource.Scope.Identity));
        command.Parameters.Add(
            SqlParameter(
                "@ResourceVersion",
                SqlDbType.BigInt,
                resource.Version.Value));
        command.Parameters.Add(
            GuidParameter(
                "@CreatedByPrincipalId",
                resource.Provenance.CreatedBy.Value));
        command.Parameters.Add(
            DateTimeParameter(
                "@CreatedAtUtc",
                resource.Provenance.CreatedAtUtc));
        command.Parameters.Add(
            GuidParameter(
                "@CorrelationId",
                resource.Provenance.CorrelationId.Value));
        command.Parameters.Add(
            GuidParameter(
                "@CausationId",
                resource.Provenance.CausationId?.Value));
        command.Parameters.Add(
            IntParameter(
                "@LifecycleStatus",
                (int)resource.Lifecycle.Status));
        command.Parameters.Add(
            DateTimeParameter(
                "@LifecycleChangedAtUtc",
                resource.Lifecycle.ChangedAtUtc));
        command.Parameters.Add(
            SqlParameter(
                "@MetadataJson",
                SqlDbType.NVarChar,
                -1,
                SerializeMetadata(resource.Metadata)));
    }


    protected static SqlParameter GuidParameter(
        string name,
        Guid? value) =>
        new(name, SqlDbType.UniqueIdentifier)
        {
            Value = (object?)value ?? DBNull.Value
        };


    protected static SqlParameter IntParameter(
        string name,
        int value) =>
        new(name, SqlDbType.Int)
        {
            Value = value
        };


    protected static SqlParameter DateTimeParameter(
        string name,
        DateTimeOffset value) =>
        new(name, SqlDbType.DateTime2)
        {
            Value = value.UtcDateTime
        };


    protected static SqlParameter SqlParameter(
        string name,
        SqlDbType type,
        int size,
        object? value) =>
        new(name, type, size)
        {
            Value = value ?? DBNull.Value
        };


    protected static void AddAccessParameters(
        SqlCommand command,
        ResourceAccessContext accessContext)
    {
        command.Parameters.Add(
            GuidParameter(
                "@PrincipalId",
                accessContext.PrincipalId!.Value.Value));
        command.Parameters.Add(
            GuidParameter(
                "@TenantId",
                accessContext.TenantId?.Value));
        command.Parameters.Add(
            GuidParameter(
                "@UserId",
                accessContext.UserId?.Value));
        command.Parameters.Add(
            GuidParameter(
                "@WorkspaceId",
                accessContext.WorkspaceId?.Value));
        command.Parameters.Add(
            GuidParameter(
                "@AgentId",
                accessContext.AgentId?.Value));
        command.Parameters.Add(
            GuidParameter(
                "@RuntimeId",
                accessContext.RuntimeId?.Value));
        command.Parameters.Add(
            GuidParameter(
                "@ExecutionId",
                accessContext.ExecutionId?.Value));
        command.Parameters.Add(
            IntParameter(
                "@GlobalScope",
                (int)ResourceScopeKind.Global));
        command.Parameters.Add(
            IntParameter(
                "@TenantScope",
                (int)ResourceScopeKind.Tenant));
        command.Parameters.Add(
            IntParameter(
                "@UserScope",
                (int)ResourceScopeKind.User));
        command.Parameters.Add(
            IntParameter(
                "@WorkspaceScope",
                (int)ResourceScopeKind.Workspace));
        command.Parameters.Add(
            IntParameter(
                "@AgentScope",
                (int)ResourceScopeKind.Agent));
        command.Parameters.Add(
            IntParameter(
                "@RuntimeScope",
                (int)ResourceScopeKind.Runtime));
        command.Parameters.Add(
            IntParameter(
                "@ExecutionScope",
                (int)ResourceScopeKind.Execution));
    }

    private const string ScopeAccessPredicate = """
        (
            [ScopeKind] = @GlobalScope
            OR ([ScopeKind] = @TenantScope AND @TenantId IS NOT NULL AND [ScopeIdentity] = @TenantId)
            OR ([ScopeKind] = @UserScope AND @UserId IS NOT NULL AND [ScopeIdentity] = @UserId)
            OR ([ScopeKind] = @WorkspaceScope AND @WorkspaceId IS NOT NULL AND [ScopeIdentity] = @WorkspaceId)
            OR ([ScopeKind] = @AgentScope AND @AgentId IS NOT NULL AND [ScopeIdentity] = @AgentId)
            OR ([ScopeKind] = @RuntimeScope AND @RuntimeId IS NOT NULL AND [ScopeIdentity] = @RuntimeId)
            OR ([ScopeKind] = @ExecutionScope AND @ExecutionId IS NOT NULL AND [ScopeIdentity] = @ExecutionId)
        )
        """;


    protected static Error NotFound(string code, string message) =>
        new(code, ErrorCategory.NotFound, message);


    protected static Error Conflict(string code, string message) =>
        new(code, ErrorCategory.Conflict, message);


    protected static Error Forbidden(string code, string message) =>
        new(code, ErrorCategory.Forbidden, message);


    protected static Error Concurrency(string code, string message) =>
        new(code, ErrorCategory.Concurrency, message);


    protected static Error ToSqlError(
        string resourceName,
        SqlException exception) =>
        HivePersistenceError.External(
            $"hive.persistence.{resourceName.Replace(' ', '-')}.sql-failure",
            $"SQL Server operation for the {resourceName} failed.",
            exception);


    protected static Error ToInvalidStateError(
        string resourceName,
        Exception exception) =>
        HivePersistenceError.Internal(
            $"hive.persistence.{resourceName.Replace(' ', '-')}.invalid-state",
            $"Persisted {resourceName} state could not be read or validated.",
            exception);

    private sealed record CapabilityPersistenceItem(
        string Key,
        CapabilityState State);

    private sealed class ConcurrencyException : Exception
    {
    }


    protected static bool IsConstraintConflict(SqlException exception) =>
        exception.Number is 2601 or 2627;
}

    protected sealed record CapabilityPersistenceItem(
        string Key,
        CapabilityState State);

    protected sealed class ConcurrencyException : Exception
    {
    }
}
