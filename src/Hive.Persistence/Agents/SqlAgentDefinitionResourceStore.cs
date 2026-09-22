using System.Data;
using System.Text.Json;
using Hive.Agents;
using Hive.Core;
using Microsoft.Data.SqlClient;

namespace Hive.Persistence;

public sealed class SqlAgentDefinitionResourceStore : IAgentDefinitionResourceStore
{
    private const string Columns = """
        [AgentDefinitionId],
        [DefinitionKey],
        [DisplayName],
        [Generation],
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

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.General);

    private readonly HiveDatabaseOptions _options;

    public SqlAgentDefinitionResourceStore(HiveDatabaseOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<Result<AgentDefinition>> CreateAgentDefinitionAsync(
        AgentDefinition definition,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(
            "agent definition",
            cancellationToken,
            async (connection, transaction) =>
            {
                ArgumentNullException.ThrowIfNull(definition);

                var resource = definition.Resource;
                if (resource is null)
                {
                    return Result<AgentDefinition>.Failure(
                        Error.Validation(
                            "hive.agent-definition.resource-required",
                            "A persisted AgentDefinition requires a resource envelope."));
                }

                var validation = ValidateCreate(resource, accessContext);
                if (validation is not null)
                    return Result<AgentDefinition>.Failure(validation);

                await using var command = CreateCommand(
                    connection,
                    $"""
                    INSERT INTO [dbo].[HiveAgentDefinitions]
                    (
                        {Columns}
                    )
                    VALUES
                    (
                        @AgentDefinitionId,
                        @DefinitionKey,
                        @DisplayName,
                        @Generation,
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
                    """,
                    transaction);

                AddParameters(command, definition);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                return Result<AgentDefinition>.Success(definition);
            });

    public Task<Result<AgentDefinition>> GetAgentDefinitionAsync(
        AgentDefinitionId agentDefinitionId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            "agent definition",
            cancellationToken,
            async connection =>
            {
                ValidateAccessContext(accessContext);

                if (agentDefinitionId == default)
                {
                    return Result<AgentDefinition>.Failure(
                        Error.Validation(
                            "hive.agent-definition.identity-required",
                            "AgentDefinition identity is required."));
                }

                await using var command = CreateCommand(
                    connection,
                    $"""
                    SELECT {Columns}
                    FROM [dbo].[HiveAgentDefinitions]
                    WHERE [AgentDefinitionId] = @AgentDefinitionId;
                    """);

                command.Parameters.Add(
                    GuidParameter(
                        "@AgentDefinitionId",
                        agentDefinitionId.Value));

                await using var reader =
                    await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return Result<AgentDefinition>.Failure(
                        NotFound(
                            "hive.agent-definition.not-found",
                            "The requested AgentDefinition does not exist."));
                }

                var definition = ReadDefinition(reader);
                var accessError = ValidateAccess(
                    definition.Resource!,
                    accessContext);

                return accessError is null
                    ? Result<AgentDefinition>.Success(definition)
                    : Result<AgentDefinition>.Failure(accessError);
            });

    public Task<Result<IReadOnlyList<AgentDefinition>>> ListAgentDefinitionsAsync(
        ResourceAccessContext accessContext,
        bool includeRetired = false,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            "agent definition",
            cancellationToken,
            async connection =>
            {
                ValidateAccessContext(accessContext);

                await using var command = CreateCommand(
                    connection,
                    $"""
                    SELECT {Columns}
                    FROM [dbo].[HiveAgentDefinitions]
                    WHERE [OwnerPrincipalId] = @PrincipalId
                      AND {ScopeAccessPredicate}
                    {(includeRetired
                        ? string.Empty
                        : "AND [LifecycleStatus] <> @RetiredLifecycle")}
                    ORDER BY [DisplayName], [AgentDefinitionId];
                    """);

                AddAccessParameters(command, accessContext);

                if (!includeRetired)
                {
                    command.Parameters.Add(
                        IntParameter(
                            "@RetiredLifecycle",
                            (int)ResourceLifecycleStatus.Retired));
                }

                var items = new List<AgentDefinition>();

                await using var reader =
                    await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    items.Add(ReadDefinition(reader));

                return Result<IReadOnlyList<AgentDefinition>>.Success(items);
            });

    public Task<Result<AgentDefinition>> UpdateAgentDefinitionAsync(
        AgentDefinition definition,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(
            "agent definition",
            cancellationToken,
            async (connection, transaction) =>
            {
                ArgumentNullException.ThrowIfNull(definition);

                var resource = definition.Resource;
                if (resource is null)
                {
                    return Result<AgentDefinition>.Failure(
                        Error.Validation(
                            "hive.agent-definition.resource-required",
                            "A persisted AgentDefinition requires a resource envelope."));
                }

                var validation = ValidateUpdate(
                    resource,
                    accessContext);

                if (validation is not null)
                    return Result<AgentDefinition>.Failure(validation);

                var current = await LoadDefinitionAsync(
                    connection,
                    transaction,
                    definition.Id,
                    cancellationToken).ConfigureAwait(false);

                if (current is null)
                {
                    return Result<AgentDefinition>.Failure(
                        NotFound(
                            "hive.agent-definition.not-found",
                            "The requested AgentDefinition does not exist."));
                }

                var accessError = ValidateAccess(
                    current.Resource!,
                    accessContext);

                if (accessError is not null)
                    return Result<AgentDefinition>.Failure(accessError);

                if (resource.Version != current.Resource!.Version)
                {
                    return Result<AgentDefinition>.Failure(
                        Error.Concurrency(
                            "hive.agent-definition.concurrency",
                            "The AgentDefinition version is stale."));
                }

                if (current.Resource.Lifecycle.Status == ResourceLifecycleStatus.Retired)
                {
                    return Result<AgentDefinition>.Failure(
                        Error.Conflict(
                            "hive.agent-definition.retired",
                            "A retired AgentDefinition cannot be updated."));
                }

                if (!string.Equals(
                    definition.Key,
                    current.Key,
                    StringComparison.Ordinal))
                {
                    return Result<AgentDefinition>.Failure(
                        Error.Conflict(
                            "hive.agent-definition.key-immutable",
                            "AgentDefinition key cannot be changed after creation."));
                }

                var updatedResource = new ResourceEnvelope<AgentDefinitionId>(
                    ResourceKind.AgentDefinition,
                    current.Id,
                    current.Resource.Owner,
                    current.Resource.Scope,
                    current.Resource.Version.Next(),
                    current.Resource.Provenance,
                    current.Resource.Lifecycle,
                    resource.Metadata);

                var updated = new AgentDefinition(
                    updatedResource,
                    current.Key,
                    definition.DisplayName,
                    definition.Generation);

                await UpdateRowAsync(
                    connection,
                    transaction,
                    current,
                    updated,
                    cancellationToken).ConfigureAwait(false);

                return Result<AgentDefinition>.Success(updated);
            });

    public Task<Result<AgentDefinition>> DeleteAgentDefinitionAsync(
        AgentDefinitionId agentDefinitionId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(
            "agent definition",
            cancellationToken,
            async (connection, transaction) =>
            {
                ValidateAccessContext(accessContext);

                if (agentDefinitionId == default)
                {
                    return Result<AgentDefinition>.Failure(
                        Error.Validation(
                            "hive.agent-definition.identity-required",
                            "AgentDefinition identity is required."));
                }

                var current = await LoadDefinitionAsync(
                    connection,
                    transaction,
                    agentDefinitionId,
                    cancellationToken).ConfigureAwait(false);

                if (current is null)
                {
                    return Result<AgentDefinition>.Failure(
                        NotFound(
                            "hive.agent-definition.not-found",
                            "The requested AgentDefinition does not exist."));
                }

                var accessError = ValidateAccess(
                    current.Resource!,
                    accessContext);

                if (accessError is not null)
                    return Result<AgentDefinition>.Failure(accessError);

                if (current.Resource!.Lifecycle.Status == ResourceLifecycleStatus.Retired)
                    return Result<AgentDefinition>.Success(current);

                var retired = new AgentDefinition(
                    current.Resource.TransitionLifecycle(
                        ResourceLifecycleStatus.Retired,
                        DateTimeOffset.UtcNow),
                    current.Key,
                    current.DisplayName,
                    current.Generation);

                await UpdateRetiredAsync(
                    connection,
                    transaction,
                    retired,
                    current.Resource.Version,
                    cancellationToken).ConfigureAwait(false);

                return Result<AgentDefinition>.Success(retired);
            });

    private async Task<AgentDefinition?> LoadDefinitionAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        AgentDefinitionId id,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            $"""
            SELECT {Columns}
            FROM [dbo].[HiveAgentDefinitions]
            WHERE [AgentDefinitionId] = @AgentDefinitionId;
            """,
            transaction);

        command.Parameters.Add(GuidParameter("@AgentDefinitionId", id.Value));

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadDefinition(reader)
            : null;
    }

    private async Task UpdateRowAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        AgentDefinition current,
        AgentDefinition updated,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            """
            UPDATE [dbo].[HiveAgentDefinitions]
            SET
                [DisplayName] = @DisplayName,
                [Generation] = @Generation,
                [ResourceVersion] = @NewVersion,
                [MetadataJson] = @MetadataJson
            WHERE [AgentDefinitionId] = @AgentDefinitionId
              AND [ResourceVersion] = @ExpectedVersion;
            """,
            transaction);

        command.Parameters.Add(
            SqlParameter(
                "@DisplayName",
                SqlDbType.NVarChar,
                200,
                updated.DisplayName));
        command.Parameters.Add(
            IntParameter(
                "@Generation",
                (int)updated.Generation));
        command.Parameters.Add(
            BigIntParameter(
                "@NewVersion",
                updated.Resource!.Version.Value));
        command.Parameters.Add(
            SqlParameter(
                "@MetadataJson",
                SqlDbType.NVarChar,
                -1,
                SerializeMetadata(updated.Resource.Metadata)));
        command.Parameters.Add(
            GuidParameter(
                "@AgentDefinitionId",
                updated.Id.Value));
        command.Parameters.Add(
            BigIntParameter(
                "@ExpectedVersion",
                current.Resource!.Version.Value));

        if (await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
            throw new ConcurrencyException();
    }

    private async Task UpdateRetiredAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        AgentDefinition retired,
        ResourceVersion expectedVersion,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            """
            UPDATE [dbo].[HiveAgentDefinitions]
            SET
                [ResourceVersion] = @NewVersion,
                [LifecycleStatus] = @LifecycleStatus,
                [LifecycleChangedAtUtc] = @LifecycleChangedAtUtc
            WHERE [AgentDefinitionId] = @AgentDefinitionId
              AND [ResourceVersion] = @ExpectedVersion;
            """,
            transaction);

        command.Parameters.Add(
            BigIntParameter(
                "@NewVersion",
                retired.Resource!.Version.Value));
        command.Parameters.Add(
            IntParameter(
                "@LifecycleStatus",
                (int)retired.Resource.Lifecycle.Status));
        command.Parameters.Add(
            DateTimeParameter(
                "@LifecycleChangedAtUtc",
                retired.Resource.Lifecycle.ChangedAtUtc));
        command.Parameters.Add(
            GuidParameter(
                "@AgentDefinitionId",
                retired.Id.Value));
        command.Parameters.Add(
            BigIntParameter(
                "@ExpectedVersion",
                expectedVersion.Value));

        if (await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
            throw new ConcurrencyException();
    }

    private static AgentDefinition ReadDefinition(SqlDataReader reader)
    {
        var resource = ReadResourceEnvelope(
            reader,
            ResourceKind.AgentDefinition,
            "AgentDefinitionId",
            static id => new AgentDefinitionId(id));

        var generation = (AgentGeneration)reader.GetInt32(
            reader.GetOrdinal("Generation"));

        if (!Enum.IsDefined(generation))
            throw new InvalidOperationException(
                "Persisted AgentDefinition generation is invalid.");

        return new AgentDefinition(
            resource,
            reader.GetString(reader.GetOrdinal("DefinitionKey")),
            reader.GetString(reader.GetOrdinal("DisplayName")),
            generation);
    }

    private static ResourceEnvelope<TIdentity> ReadResourceEnvelope<TIdentity>(
        SqlDataReader reader,
        ResourceKind expectedKind,
        string identityColumn,
        Func<Guid, TIdentity> identityFactory)
        where TIdentity : struct
    {
        var scopeKind = (ResourceScopeKind)reader.GetInt32(
            reader.GetOrdinal("ScopeKind"));

        Guid? scopeIdentity = reader.IsDBNull(
            reader.GetOrdinal("ScopeIdentity"))
            ? null
            : reader.GetGuid(reader.GetOrdinal("ScopeIdentity"));

        if (!Enum.IsDefined(scopeKind))
            throw new InvalidOperationException(
                "Persisted resource scope kind is invalid.");

        var scope = scopeKind switch
        {
            ResourceScopeKind.Global when scopeIdentity is null =>
                ResourceScope.Global(),
            ResourceScopeKind.Tenant when scopeIdentity is not null =>
                ResourceScope.Tenant(new TenantId(scopeIdentity.Value)),
            ResourceScopeKind.User when scopeIdentity is not null =>
                ResourceScope.User(new UserId(scopeIdentity.Value)),
            ResourceScopeKind.Workspace when scopeIdentity is not null =>
                ResourceScope.Workspace(new WorkspaceId(scopeIdentity.Value)),
            ResourceScopeKind.Agent when scopeIdentity is not null =>
                ResourceScope.Agent(new AgentId(scopeIdentity.Value)),
            ResourceScopeKind.Runtime when scopeIdentity is not null =>
                ResourceScope.Runtime(new RuntimeId(scopeIdentity.Value)),
            ResourceScopeKind.Execution when scopeIdentity is not null =>
                ResourceScope.Execution(new ExecutionId(scopeIdentity.Value)),
            _ => throw new InvalidOperationException(
                "Persisted resource scope state is invalid.")
        };

        var lifecycleStatus = (ResourceLifecycleStatus)reader.GetInt32(
            reader.GetOrdinal("LifecycleStatus"));

        if (!Enum.IsDefined(lifecycleStatus))
            throw new InvalidOperationException(
                "Persisted resource lifecycle state is invalid.");

        var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(
            reader.GetString(reader.GetOrdinal("MetadataJson")),
            JsonOptions);

        if (metadata is null)
            throw new InvalidOperationException(
                "Persisted resource metadata is invalid.");

        return new ResourceEnvelope<TIdentity>(
            expectedKind,
            identityFactory(reader.GetGuid(reader.GetOrdinal(identityColumn))),
            new PrincipalId(
                reader.GetGuid(reader.GetOrdinal("OwnerPrincipalId"))),
            scope,
            new ResourceVersion(
                reader.GetInt64(reader.GetOrdinal("ResourceVersion"))),
            new ResourceProvenance(
                new PrincipalId(
                    reader.GetGuid(reader.GetOrdinal("CreatedByPrincipalId"))),
                reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc")),
                new CorrelationId(
                    reader.GetGuid(reader.GetOrdinal("CorrelationId"))),
                reader.IsDBNull(reader.GetOrdinal("CausationId"))
                    ? null
                    : new CausationId(
                        reader.GetGuid(reader.GetOrdinal("CausationId")))),
            new ResourceLifecycle(
                lifecycleStatus,
                reader.GetDateTime(reader.GetOrdinal("LifecycleChangedAtUtc"))),
            metadata);
    }

    private static Error? ValidateCreate(
        ResourceEnvelope<AgentDefinitionId> resource,
        ResourceAccessContext accessContext)
    {
        ValidateAccessContext(accessContext);

        if (resource.Kind != ResourceKind.AgentDefinition)
            return Error.Validation(
                "hive.resource.kind-invalid",
                "The agent definition resource kind is invalid.");

        if (resource.Version != ResourceVersion.Initial)
            return Error.Validation(
                "hive.resource.version-invalid",
                "A new agent definition must start at resource version 1.");

        if (resource.Lifecycle.Status != ResourceLifecycleStatus.Active)
            return Error.Validation(
                "hive.resource.lifecycle-invalid",
                "A new agent definition must start in Active lifecycle state.");

        return ValidateAccess(resource, accessContext);
    }

    private static Error? ValidateUpdate(
        ResourceEnvelope<AgentDefinitionId> resource,
        ResourceAccessContext accessContext)
    {
        ValidateAccessContext(accessContext);

        if (resource.Kind != ResourceKind.AgentDefinition)
            return Error.Validation(
                "hive.resource.kind-invalid",
                "The agent definition resource kind is invalid.");

        if (resource.Version.Value <= 0)
            return Error.Validation(
                "hive.resource.version-invalid",
                "The agent definition resource version is invalid.");

        return ValidateAccess(resource, accessContext);
    }

    private static Error? ValidateAccess(
        ResourceEnvelope<AgentDefinitionId> resource,
        ResourceAccessContext accessContext)
    {
        if (resource.Owner != accessContext.PrincipalId)
        {
            return new Error(
                "hive.resource.owner-forbidden",
                ErrorCategory.Forbidden,
                "The current principal does not own the agent definition.");
        }

        return resource.Scope.Matches(accessContext)
            ? null
            : new Error(
                "hive.resource.scope-forbidden",
                ErrorCategory.Forbidden,
                "The current access context is outside the agent definition scope.");
    }

    private static void ValidateAccessContext(
        ResourceAccessContext accessContext)
    {
        ArgumentNullException.ThrowIfNull(accessContext);

        if (accessContext.PrincipalId is null ||
            accessContext.DeploymentId is null)
        {
            throw new InvalidOperationException(
                "A deployment and principal are required for agent definition access.");
        }
    }

    private async Task<Result<T>> ExecuteAsync<T>(
        string resourceName,
        CancellationToken cancellationToken,
        Func<SqlConnection, Task<Result<T>>> operation)
    {
        try
        {
            await using var connection =
                await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

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
                    $"hive.{resourceName.Replace(' ', '-')}.duplicate",
                    $"The {resourceName} identity or key already exists."));
        }
        catch (SqlException exception)
        {
            return Result<T>.Failure(
                new Error(
                    $"hive.persistence.{resourceName.Replace(' ', '-')}.sql-failure",
                    ErrorCategory.External,
                    $"SQL Server operation for the {resourceName} failed: {exception.Message}"));
        }
        catch (Exception exception)
        {
            return Result<T>.Failure(
                new Error(
                    $"hive.persistence.{resourceName.Replace(' ', '-')}.invalid-state",
                    ErrorCategory.Internal,
                    $"Persisted {resourceName} state could not be read or validated: {exception.Message}"));
        }
    }

    private async Task<Result<T>> ExecuteInTransactionAsync<T>(
        string resourceName,
        CancellationToken cancellationToken,
        Func<SqlConnection, SqlTransaction, Task<Result<T>>> operation)
    {
        try
        {
            await using var connection =
                await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

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
                    $"hive.{resourceName.Replace(' ', '-')}.duplicate",
                    $"The {resourceName} identity or key already exists."));
        }
        catch (SqlException exception)
        {
            return Result<T>.Failure(
                new Error(
                    $"hive.persistence.{resourceName.Replace(' ', '-')}.sql-failure",
                    ErrorCategory.External,
                    $"SQL Server operation for the {resourceName} failed: {exception.Message}"));
        }
        catch (ConcurrencyException)
        {
            return Result<T>.Failure(
                Error.Concurrency(
                    $"hive.{resourceName.Replace(' ', '-')}.concurrency",
                    $"The {resourceName} changed before the operation completed."));
        }
        catch (Exception exception)
        {
            return Result<T>.Failure(
                new Error(
                    $"hive.persistence.{resourceName.Replace(' ', '-')}.invalid-state",
                    ErrorCategory.Internal,
                    $"Persisted {resourceName} state could not be read or validated: {exception.Message}"));
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

    private SqlCommand CreateCommand(
        SqlConnection connection,
        string commandText,
        SqlTransaction? transaction = null) =>
        new(commandText, connection, transaction)
        {
            CommandTimeout = _options.CommandTimeoutSeconds
        };

    private static void AddParameters(
        SqlCommand command,
        AgentDefinition definition)
    {
        var resource = definition.Resource!;

        command.Parameters.Add(
            GuidParameter(
                "@AgentDefinitionId",
                definition.Id.Value));
        command.Parameters.Add(
            SqlParameter(
                "@DefinitionKey",
                SqlDbType.NVarChar,
                100,
                definition.Key));
        command.Parameters.Add(
            SqlParameter(
                "@DisplayName",
                SqlDbType.NVarChar,
                200,
                definition.DisplayName));
        command.Parameters.Add(
            IntParameter(
                "@Generation",
                (int)definition.Generation));
        AddResourceParameters(command, resource);
    }

    private static void AddResourceParameters(
        SqlCommand command,
        ResourceEnvelope<AgentDefinitionId> resource)
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
            BigIntParameter(
                "@ResourceVersion",
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

    private static void AddAccessParameters(
        SqlCommand command,
        ResourceAccessContext accessContext)
    {
        command.Parameters.Add(
            GuidParameter(
                "@PrincipalId",
                accessContext.PrincipalId!.Value.Value));
        command.Parameters.Add(
            GuidParameter("@TenantId", accessContext.TenantId?.Value));
        command.Parameters.Add(
            GuidParameter("@UserId", accessContext.UserId?.Value));
        command.Parameters.Add(
            GuidParameter("@WorkspaceId", accessContext.WorkspaceId?.Value));
        command.Parameters.Add(
            GuidParameter("@AgentId", accessContext.AgentId?.Value));
        command.Parameters.Add(
            GuidParameter("@RuntimeId", accessContext.RuntimeId?.Value));
        command.Parameters.Add(
            GuidParameter("@ExecutionId", accessContext.ExecutionId?.Value));
        command.Parameters.Add(
            IntParameter("@GlobalScope", (int)ResourceScopeKind.Global));
        command.Parameters.Add(
            IntParameter("@TenantScope", (int)ResourceScopeKind.Tenant));
        command.Parameters.Add(
            IntParameter("@UserScope", (int)ResourceScopeKind.User));
        command.Parameters.Add(
            IntParameter("@WorkspaceScope", (int)ResourceScopeKind.Workspace));
        command.Parameters.Add(
            IntParameter("@AgentScope", (int)ResourceScopeKind.Agent));
        command.Parameters.Add(
            IntParameter("@RuntimeScope", (int)ResourceScopeKind.Runtime));
        command.Parameters.Add(
            IntParameter("@ExecutionScope", (int)ResourceScopeKind.Execution));
    }

    private const string ScopeAccessPredicate = """
        (
            [ScopeKind] = @GlobalScope
            OR ([ScopeKind] = @TenantScope
                AND @TenantId IS NOT NULL
                AND [ScopeIdentity] = @TenantId)
            OR ([ScopeKind] = @UserScope
                AND @UserId IS NOT NULL
                AND [ScopeIdentity] = @UserId)
            OR ([ScopeKind] = @WorkspaceScope
                AND @WorkspaceId IS NOT NULL
                AND [ScopeIdentity] = @WorkspaceId)
            OR ([ScopeKind] = @AgentScope
                AND @AgentId IS NOT NULL
                AND [ScopeIdentity] = @AgentId)
            OR ([ScopeKind] = @RuntimeScope
                AND @RuntimeId IS NOT NULL
                AND [ScopeIdentity] = @RuntimeId)
            OR ([ScopeKind] = @ExecutionScope
                AND @ExecutionId IS NOT NULL
                AND [ScopeIdentity] = @ExecutionId)
        )
        """;

    private static Error NotFound(
        string code,
        string message) =>
        new(code, ErrorCategory.NotFound, message);

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

    private static SqlParameter DateTimeParameter(
        string name,
        DateTimeOffset value) =>
        new(name, SqlDbType.DateTime2)
        {
            Value = value.UtcDateTime
        };

    private static SqlParameter SqlParameter(
        string name,
        SqlDbType type,
        int size,
        object? value) =>
        new(name, type, size)
        {
            Value = value ?? DBNull.Value
        };

    private static string SerializeMetadata(
        IReadOnlyDictionary<string, string> metadata) =>
        JsonSerializer.Serialize(metadata, JsonOptions);

    private sealed class ConcurrencyException : Exception
    {
    }

    private static bool IsConstraintConflict(SqlException exception) =>
        exception.Number is 2601 or 2627;
}
