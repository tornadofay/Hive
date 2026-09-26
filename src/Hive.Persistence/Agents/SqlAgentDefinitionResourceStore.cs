using System.Data;
using Hive.Agents;
using Hive.Core;
using Microsoft.Data.SqlClient;

namespace Hive.Persistence;

public sealed class SqlAgentDefinitionResourceStore : SqlResourceStoreBase, IAgentDefinitionResourceStore
{
    private const string Columns = """
        [AgentDefinitionId],
        [DefinitionKey],
        [DisplayName],
        [Generation],
        [ConfiguredExecutionTargetId],
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

    public SqlAgentDefinitionResourceStore(HiveDatabaseOptions options)
        : base(options)
    {
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

                var validation = ValidateCreate(
                    resource,
                    ResourceKind.AgentDefinition,
                    accessContext,
                    "agent definition");
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
                        @ConfiguredExecutionTargetId,
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
                var accessError = ValidateAccess(definition.Resource!, accessContext, "agent definition");

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
                    ResourceKind.AgentDefinition,
                    accessContext,
                    "agent definition");

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

                var accessError = ValidateAccess(current.Resource!, accessContext, "agent definition");

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
                    definition.Generation,
                    definition.ConfiguredExecutionTargetId);

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

                var accessError = ValidateAccess(current.Resource!, accessContext, "agent definition");

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
                    current.Generation,
                    current.ConfiguredExecutionTargetId);

                await UpdateLifecycleAsync(
                    connection,
                    transaction,
                    "HiveAgentDefinitions",
                    "AgentDefinitionId",
                    retired.Id.Value,
                    retired.Resource!,
                    current.Resource.Version,
                    cancellationToken).ConfigureAwait(false);

                return Result<AgentDefinition>.Success(retired);
            });

    public Task<Result<AgentDefinition>> ReactivateAgentDefinitionAsync(
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

                var accessError = ValidateAccess(current.Resource!, accessContext, "agent definition");

                if (accessError is not null)
                    return Result<AgentDefinition>.Failure(accessError);

                if (current.Resource!.Lifecycle.Status == ResourceLifecycleStatus.Active)
                    return Result<AgentDefinition>.Success(current);

                if (current.Resource.Lifecycle.Status != ResourceLifecycleStatus.Retired)
                {
                    return Result<AgentDefinition>.Failure(
                        Error.Conflict(
                            "hive.agent-definition.lifecycle-invalid",
                            "Only a retired AgentDefinition can be reactivated."));
                }

                var active = new AgentDefinition(
                    current.Resource.TransitionLifecycle(
                        ResourceLifecycleStatus.Active,
                        DateTimeOffset.UtcNow),
                    current.Key,
                    current.DisplayName,
                    current.Generation,
                    current.ConfiguredExecutionTargetId);

                await UpdateLifecycleAsync(
                    connection,
                    transaction,
                    "HiveAgentDefinitions",
                    "AgentDefinitionId",
                    active.Id.Value,
                    active.Resource!,
                    current.Resource.Version,
                    cancellationToken).ConfigureAwait(false);

                return Result<AgentDefinition>.Success(active);
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
                [ConfiguredExecutionTargetId] = @ConfiguredExecutionTargetId,
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
            GuidParameter(
                "@ConfiguredExecutionTargetId",
                updated.ConfiguredExecutionTargetId?.Value));
        command.Parameters.Add(
            SqlParameter("@NewVersion", SqlDbType.BigInt, updated.Resource!.Version.Value));
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
            SqlParameter("@ExpectedVersion", SqlDbType.BigInt, current.Resource!.Version.Value));

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
            generation,
            reader.IsDBNull(reader.GetOrdinal("ConfiguredExecutionTargetId"))
                ? null
                : new ExecutionTargetId(
                    reader.GetGuid(reader.GetOrdinal("ConfiguredExecutionTargetId"))));
    }

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
        command.Parameters.Add(
            GuidParameter(
                "@ConfiguredExecutionTargetId",
                definition.ConfiguredExecutionTargetId?.Value));
        AddResourceParameters(command, resource);
    }

}