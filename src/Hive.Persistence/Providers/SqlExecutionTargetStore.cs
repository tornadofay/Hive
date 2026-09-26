using System.Data;
using Hive.Core;
using Microsoft.Data.SqlClient;

namespace Hive.Persistence;

internal sealed class SqlExecutionTargetStore : SqlResourceStoreBase
{
    private readonly SqlProviderResourceReader _reader;

    private const string ExecutionTargetColumns = """
        [ExecutionTargetId],
        [ProviderId],
        [ProviderAccountId],
        [TargetKey],
        [DisplayName],
        [EndpointUri],
        [Model],
        [Deployment],
        [CapabilitiesJson],
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

    internal SqlExecutionTargetStore(HiveDatabaseOptions options, SqlProviderResourceReader reader)
        : base(options)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    internal Task<Result<ExecutionTarget>> CreateExecutionTargetAsync(
        ExecutionTarget target,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(
            "execution target",
            cancellationToken,
            async (connection, transaction) =>
            {
                var validation = ValidateCreate(
                    target.Resource,
                    ResourceKind.ExecutionTarget,
                    accessContext,
                    "execution target");

                if (validation is not null)
                    return Result<ExecutionTarget>.Failure(validation);

                var provider = await _reader.LoadProviderAsync(
                    connection,
                    transaction,
                    target.ProviderId,
                    cancellationToken).ConfigureAwait(false);

                if (provider is null)
                {
                    return Result<ExecutionTarget>.Failure(
                        NotFound(
                            "hive.provider.not-found",
                            "The requested provider does not exist."));
                }

                var providerAccessError = ValidateAccess(
                    provider.Resource,
                    accessContext,
                    "provider");

                if (providerAccessError is not null)
                    return Result<ExecutionTarget>.Failure(providerAccessError);

                var account = await _reader.LoadProviderAccountAsync(
                    connection,
                    transaction,
                    target.ProviderAccountId,
                    cancellationToken).ConfigureAwait(false);

                if (account is null)
                {
                    return Result<ExecutionTarget>.Failure(
                        NotFound(
                            "hive.provider-account.not-found",
                            "The requested provider account does not exist."));
                }

                var accountAccessError = ValidateAccess(
                    account.Resource,
                    accessContext,
                    "provider account");

                if (accountAccessError is not null)
                    return Result<ExecutionTarget>.Failure(accountAccessError);

                if (provider.Resource.Lifecycle.Status == ResourceLifecycleStatus.Retired ||
                    account.Resource.Lifecycle.Status == ResourceLifecycleStatus.Retired)
                {
                    return Result<ExecutionTarget>.Failure(
                        Conflict(
                            "hive.execution-target.parent-retired",
                            "An execution target cannot be created under a retired provider or provider account."));
                }

                if (account.ProviderId != target.ProviderId)
                {
                    return Result<ExecutionTarget>.Failure(
                        Error.Validation(
                            "hive.execution-target.provider-mismatch",
                            "The provider account belongs to a different provider."));
                }

                await InsertExecutionTargetAsync(
                    connection,
                    transaction,
                    target,
                    cancellationToken).ConfigureAwait(false);

                return Result<ExecutionTarget>.Success(target);
            });


    internal Task<Result<ExecutionTarget>> GetExecutionTargetAsync(
        ExecutionTargetId executionTargetId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            "execution target",
            cancellationToken,
            async connection =>
            {
                ValidateAccessContext(accessContext);

                await using var command = CreateCommand(
                    connection,
                    $"""
                    SELECT {ExecutionTargetColumns}
                    FROM [dbo].[HiveExecutionTargets]
                    WHERE [ExecutionTargetId] = @ExecutionTargetId;
                    """);

                command.Parameters.Add(
                    GuidParameter(
                        "@ExecutionTargetId",
                        executionTargetId.Value));

                await using var reader = await command.ExecuteReaderAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return Result<ExecutionTarget>.Failure(
                        NotFound(
                            "hive.execution-target.not-found",
                            "The requested execution target does not exist."));
                }

                var target = ReadExecutionTarget(reader);
                var accessError = ValidateAccess(
                    target.Resource,
                    accessContext,
                    "execution target");

                return accessError is null
                    ? Result<ExecutionTarget>.Success(target)
                    : Result<ExecutionTarget>.Failure(accessError);
            });


    internal Task<Result<IReadOnlyList<ExecutionTarget>>> ListExecutionTargetsAsync(
        ProviderAccountId providerAccountId,
        ResourceAccessContext accessContext,
        bool includeRetired = false,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            "execution target",
            cancellationToken,
            async connection =>
            {
                ValidateAccessContext(accessContext);

                await using var command = CreateCommand(
                    connection,
                    $"""
                    SELECT {ExecutionTargetColumns}
                    FROM [dbo].[HiveExecutionTargets]
                    WHERE [ProviderAccountId] = @ProviderAccountId
                      AND [OwnerPrincipalId] = @PrincipalId
                      AND {ScopeAccessPredicate}
                    {(includeRetired
                        ? string.Empty
                        : "AND [LifecycleStatus] <> @RetiredLifecycle")}
                    ORDER BY [DisplayName], [ExecutionTargetId];
                    """);

                command.Parameters.Add(
                    GuidParameter(
                        "@ProviderAccountId",
                        providerAccountId.Value));

                AddAccessParameters(command, accessContext);

                if (!includeRetired)
                {
                    command.Parameters.Add(
                        IntParameter(
                            "@RetiredLifecycle",
                            (int)ResourceLifecycleStatus.Retired));
                }

                var items = new List<ExecutionTarget>();

                await using var reader = await command.ExecuteReaderAsync(cancellationToken)
                    .ConfigureAwait(false);

                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    items.Add(ReadExecutionTarget(reader));

                return Result<IReadOnlyList<ExecutionTarget>>.Success(items);
            });


    internal Task<Result<ExecutionTarget>> UpdateExecutionTargetAsync(
        ExecutionTarget target,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(
            "execution target",
            cancellationToken,
            async (connection, transaction) =>
            {
                ValidateAccessContext(accessContext);

                var validation = ValidateUpdate(
                    target.Resource,
                    ResourceKind.ExecutionTarget,
                    accessContext,
                    "execution target");

                if (validation is not null)
                    return Result<ExecutionTarget>.Failure(validation);

                var current = await _reader.LoadExecutionTargetAsync(
                    connection,
                    transaction,
                    target.Id,
                    cancellationToken).ConfigureAwait(false);

                if (current is null)
                    return Result<ExecutionTarget>.Failure(
                        NotFound(
                            "hive.execution-target.not-found",
                            "The requested execution target does not exist."));

                var accessError = ValidateAccess(
                    current.Resource,
                    accessContext,
                    "execution target");

                if (accessError is not null)
                    return Result<ExecutionTarget>.Failure(accessError);

                if (target.Resource.Version != current.Resource.Version)
                {
                    return Result<ExecutionTarget>.Failure(
                        Concurrency(
                            "hive.execution-target.concurrency",
                            "The execution target version is stale."));
                }

                if (current.Resource.Lifecycle.Status == ResourceLifecycleStatus.Retired)
                {
                    return Result<ExecutionTarget>.Failure(
                        Conflict(
                            "hive.execution-target.retired",
                            "A retired execution target cannot be updated."));
                }

                if (target.ProviderId != current.ProviderId ||
                    target.ProviderAccountId != current.ProviderAccountId)
                {
                    return Result<ExecutionTarget>.Failure(
                        Conflict(
                            "hive.execution-target.parent-immutable",
                            "Provider and provider-account identities cannot be changed after target creation."));
                }

                if (!string.Equals(
                    target.Key,
                    current.Key,
                    StringComparison.Ordinal))
                {
                    return Result<ExecutionTarget>.Failure(
                        Conflict(
                            "hive.execution-target.key-immutable",
                            "Execution target key cannot be changed after creation."));
                }

                var updated = new ExecutionTarget(
                    new ResourceEnvelope<ExecutionTargetId>(
                        ResourceKind.ExecutionTarget,
                        current.Id,
                        current.Resource.Owner,
                        current.Resource.Scope,
                        current.Resource.Version.Next(),
                        current.Resource.Provenance,
                        current.Resource.Lifecycle,
                        target.Resource.Metadata),
                    current.ProviderId,
                    current.ProviderAccountId,
                    current.Key,
                    target.DisplayName,
                    target.Endpoint,
                    target.Model,
                    target.Deployment,
                    target.Capabilities);

                await UpdateExecutionTargetRowAsync(
                    connection,
                    transaction,
                    current,
                    updated,
                    cancellationToken).ConfigureAwait(false);

                return Result<ExecutionTarget>.Success(updated);
            });


    internal Task<Result<ExecutionTarget>> DeleteExecutionTargetAsync(
        ExecutionTargetId executionTargetId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        RetireExecutionTargetAsync(
            executionTargetId,
            accessContext,
            cancellationToken);

    internal Task<Result<ExecutionTarget>> ReactivateExecutionTargetAsync(
        ExecutionTargetId executionTargetId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        ReactivateExecutionTargetInternalAsync(
            executionTargetId,
            accessContext,
            cancellationToken);


    private Task<Result<ExecutionTarget>> ReactivateExecutionTargetInternalAsync(
        ExecutionTargetId executionTargetId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken) =>
        ExecuteInTransactionAsync(
            "execution target",
            cancellationToken,
            async (connection, transaction) =>
            {
                ValidateAccessContext(accessContext);

                var current = await _reader.LoadExecutionTargetAsync(
                    connection,
                    transaction,
                    executionTargetId,
                    cancellationToken).ConfigureAwait(false);

                if (current is null)
                    return Result<ExecutionTarget>.Failure(
                        NotFound(
                            "hive.execution-target.not-found",
                            "The requested execution target does not exist."));

                var accessError = ValidateAccess(
                    current.Resource,
                    accessContext,
                    "execution target");

                if (accessError is not null)
                    return Result<ExecutionTarget>.Failure(accessError);

                if (current.Resource.Lifecycle.Status == ResourceLifecycleStatus.Active)
                    return Result<ExecutionTarget>.Success(current);

                if (current.Resource.Lifecycle.Status != ResourceLifecycleStatus.Retired)
                    return Result<ExecutionTarget>.Failure(
                        Conflict(
                            "hive.execution-target.lifecycle-invalid",
                            "Only a retired execution target can be reactivated."));

                var provider = await _reader.LoadProviderAsync(
                    connection,
                    transaction,
                    current.ProviderId,
                    cancellationToken).ConfigureAwait(false);

                if (provider is null)
                    return Result<ExecutionTarget>.Failure(
                        NotFound(
                            "hive.provider.not-found",
                            "The provider required by this execution target no longer exists."));

                var providerAccessError = ValidateAccess(
                    provider.Resource,
                    accessContext,
                    "provider");

                if (providerAccessError is not null)
                    return Result<ExecutionTarget>.Failure(providerAccessError);

                if (provider.Resource.Lifecycle.Status != ResourceLifecycleStatus.Active)
                    return Result<ExecutionTarget>.Failure(
                        Conflict(
                            "hive.execution-target.provider-inactive",
                            "An execution target cannot be reactivated while its provider is not active."));

                var account = await _reader.LoadProviderAccountAsync(
                    connection,
                    transaction,
                    current.ProviderAccountId,
                    cancellationToken).ConfigureAwait(false);

                if (account is null)
                    return Result<ExecutionTarget>.Failure(
                        NotFound(
                            "hive.provider-account.not-found",
                            "The provider account required by this execution target no longer exists."));

                var accountAccessError = ValidateAccess(
                    account.Resource,
                    accessContext,
                    "provider account");

                if (accountAccessError is not null)
                    return Result<ExecutionTarget>.Failure(accountAccessError);

                if (account.Resource.Lifecycle.Status != ResourceLifecycleStatus.Active)
                    return Result<ExecutionTarget>.Failure(
                        Conflict(
                            "hive.execution-target.account-inactive",
                            "An execution target cannot be reactivated while its provider account is not active."));

                var active = new ExecutionTarget(
                    current.Resource.TransitionLifecycle(
                        ResourceLifecycleStatus.Active,
                        DateTimeOffset.UtcNow),
                    current.ProviderId,
                    current.ProviderAccountId,
                    current.Key,
                    current.DisplayName,
                    current.Endpoint,
                    current.Model,
                    current.Deployment,
                    current.Capabilities);

                await UpdateLifecycleAsync(
                    connection,
                    transaction,
                    "HiveExecutionTargets",
                    "ExecutionTargetId",
                    active.Id.Value,
                    active.Resource,
                    current.Resource.Version,
                    cancellationToken).ConfigureAwait(false);

                return Result<ExecutionTarget>.Success(active);
            });


    private Task<Result<ExecutionTarget>> RetireExecutionTargetAsync(
        ExecutionTargetId executionTargetId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken) =>
        ExecuteInTransactionAsync(
            "execution target",
            cancellationToken,
            async (connection, transaction) =>
            {
                ValidateAccessContext(accessContext);

                var current = await _reader.LoadExecutionTargetAsync(
                    connection,
                    transaction,
                    executionTargetId,
                    cancellationToken).ConfigureAwait(false);

                if (current is null)
                {
                    return Result<ExecutionTarget>.Failure(
                        NotFound(
                            "hive.execution-target.not-found",
                            "The requested execution target does not exist."));
                }

                var accessError = ValidateAccess(
                    current.Resource,
                    accessContext,
                    "execution target");

                if (accessError is not null)
                    return Result<ExecutionTarget>.Failure(accessError);

                if (current.Resource.Lifecycle.Status == ResourceLifecycleStatus.Retired)
                    return Result<ExecutionTarget>.Success(current);

                var retired = new ExecutionTarget(
                    current.Resource.TransitionLifecycle(
                        ResourceLifecycleStatus.Retired,
                        DateTimeOffset.UtcNow),
                    current.ProviderId,
                    current.ProviderAccountId,
                    current.Key,
                    current.DisplayName,
                    current.Endpoint,
                    current.Model,
                    current.Deployment,
                    current.Capabilities);

                await UpdateLifecycleAsync(
                    connection,
                    transaction,
                    "HiveExecutionTargets",
                    "ExecutionTargetId",
                    retired.Id.Value,
                    retired.Resource,
                    current.Resource.Version,
                    cancellationToken).ConfigureAwait(false);

                return Result<ExecutionTarget>.Success(retired);
            });


    private async Task InsertExecutionTargetAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ExecutionTarget target,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            $"""
            INSERT INTO [dbo].[HiveExecutionTargets]
            (
                {ExecutionTargetColumns}
            )
            VALUES
            (
                @ExecutionTargetId,
                @ProviderId,
                @ProviderAccountId,
                @TargetKey,
                @DisplayName,
                @EndpointUri,
                @Model,
                @Deployment,
                @CapabilitiesJson,
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

        AddExecutionTargetParameters(command, target);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }


    private async Task UpdateExecutionTargetRowAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ExecutionTarget current,
        ExecutionTarget updated,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            """
            UPDATE [dbo].[HiveExecutionTargets]
            SET [DisplayName] = @DisplayName,
                [EndpointUri] = @EndpointUri,
                [Model] = @Model,
                [Deployment] = @Deployment,
                [CapabilitiesJson] = @CapabilitiesJson,
                [ResourceVersion] = @NewVersion,
                [MetadataJson] = @MetadataJson
            WHERE [ExecutionTargetId] = @ExecutionTargetId
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
            SqlParameter(
                "@EndpointUri",
                SqlDbType.NVarChar,
                2048,
                updated.Endpoint.AbsoluteUri));
        command.Parameters.Add(
            SqlParameter(
                "@Model",
                SqlDbType.NVarChar,
                512,
                updated.Model));
        command.Parameters.Add(
            SqlParameter(
                "@Deployment",
                SqlDbType.NVarChar,
                512,
                updated.Deployment));
        command.Parameters.Add(
            SqlParameter(
                "@CapabilitiesJson",
                SqlDbType.NVarChar,
                -1,
                SerializeCapabilities(updated.Capabilities)));
        command.Parameters.Add(
            SqlParameter(
                "@NewVersion",
                SqlDbType.BigInt,
                updated.Resource.Version.Value));
        command.Parameters.Add(
            SqlParameter(
                "@ExpectedVersion",
                SqlDbType.BigInt,
                current.Resource.Version.Value));
        command.Parameters.Add(
            GuidParameter(
                "@ExecutionTargetId",
                updated.Id.Value));
        command.Parameters.Add(
            SqlParameter(
                "@MetadataJson",
                SqlDbType.NVarChar,
                -1,
                SerializeMetadata(updated.Resource.Metadata)));

        var affected = await command.ExecuteNonQueryAsync(cancellationToken)
            .ConfigureAwait(false);

        if (affected != 1)
            throw new ConcurrencyException();
    }

    private async Task UpdateLifecycleAsync<TIdentity>(
        SqlConnection connection,
        SqlTransaction transaction,
        string tableName,
        string identityColumn,
        Guid resourceId,
        ResourceEnvelope<TIdentity> updatedResource,
        ResourceVersion expectedVersion,
        CancellationToken cancellationToken)
        where TIdentity : struct
    {
        var identity = resourceId;

        await using var command = CreateCommand(
            connection,
            $"""
            UPDATE [dbo].[{tableName}]
            SET [ResourceVersion] = @NewVersion,
                [LifecycleStatus] = @LifecycleStatus,
                [LifecycleChangedAtUtc] = @LifecycleChangedAtUtc
            WHERE [{identityColumn}] = @ResourceId
              AND [ResourceVersion] = @ExpectedVersion;
            """,
            transaction);

        command.Parameters.Add(
            SqlParameter(
                "@NewVersion",
                SqlDbType.BigInt,
                updatedResource.Version.Value));
        command.Parameters.Add(
            IntParameter(
                "@LifecycleStatus",
                (int)updatedResource.Lifecycle.Status));
        command.Parameters.Add(
            DateTimeParameter(
                "@LifecycleChangedAtUtc",
                updatedResource.Lifecycle.ChangedAtUtc));
        command.Parameters.Add(GuidParameter("@ResourceId", identity));
        command.Parameters.Add(
            SqlParameter(
                "@ExpectedVersion",
                SqlDbType.BigInt,
                expectedVersion.Value));

        var affected = await command.ExecuteNonQueryAsync(cancellationToken)
            .ConfigureAwait(false);

        if (affected != 1)
            throw new ConcurrencyException();
    }


    private static ResourceEnvelope<TIdentity> ReadResourceEnvelope<TIdentity>(
        SqlDataReader reader,
        ResourceKind expectedKind,
        string identityColumn,
        Func<Guid, TIdentity> identityFactory)
        where TIdentity : struct
    {
        var identity = identityFactory(
            reader.GetGuid(reader.GetOrdinal(identityColumn)));

        var owner = new PrincipalId(
            reader.GetGuid(reader.GetOrdinal("OwnerPrincipalId")));

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

        var version = new ResourceVersion(
            reader.GetInt64(reader.GetOrdinal("ResourceVersion")));

        var provenance = new ResourceProvenance(
            new PrincipalId(
                reader.GetGuid(
                    reader.GetOrdinal("CreatedByPrincipalId"))),
            reader.GetDateTime(
                reader.GetOrdinal("CreatedAtUtc")),
            new CorrelationId(
                reader.GetGuid(reader.GetOrdinal("CorrelationId"))),
            reader.IsDBNull(reader.GetOrdinal("CausationId"))
                ? null
                : new CausationId(
                    reader.GetGuid(reader.GetOrdinal("CausationId"))));

        var lifecycleStatus = (ResourceLifecycleStatus)reader.GetInt32(
            reader.GetOrdinal("LifecycleStatus"));

        if (!Enum.IsDefined(lifecycleStatus))
        {
            throw new InvalidOperationException(
                "Persisted resource lifecycle state is invalid.");
        }

        var lifecycle = new ResourceLifecycle(
            lifecycleStatus,
            reader.GetDateTime(
                reader.GetOrdinal("LifecycleChangedAtUtc")));

        var metadata = DeserializeMetadata(
            reader.GetString(reader.GetOrdinal("MetadataJson")));

        return new ResourceEnvelope<TIdentity>(
            expectedKind,
            identity,
            owner,
            scope,
            version,
            provenance,
            lifecycle,
            metadata);
    }


    private static void AddExecutionTargetParameters(
        SqlCommand command,
        ExecutionTarget target)
    {
        AddResourceParameters(command, target.Resource);
        command.Parameters.Add(
            GuidParameter(
                "@ExecutionTargetId",
                target.Id.Value));
        command.Parameters.Add(
            GuidParameter(
                "@ProviderId",
                target.ProviderId.Value));
        command.Parameters.Add(
            GuidParameter(
                "@ProviderAccountId",
                target.ProviderAccountId.Value));
        command.Parameters.Add(
            SqlParameter(
                "@TargetKey",
                SqlDbType.NVarChar,
                100,
                target.Key));
        command.Parameters.Add(
            SqlParameter(
                "@DisplayName",
                SqlDbType.NVarChar,
                200,
                target.DisplayName));
        command.Parameters.Add(
            SqlParameter(
                "@EndpointUri",
                SqlDbType.NVarChar,
                2048,
                target.Endpoint.AbsoluteUri));
        command.Parameters.Add(
            SqlParameter(
                "@Model",
                SqlDbType.NVarChar,
                512,
                target.Model));
        command.Parameters.Add(
            SqlParameter(
                "@Deployment",
                SqlDbType.NVarChar,
                512,
                target.Deployment));
        command.Parameters.Add(
            SqlParameter(
                "@CapabilitiesJson",
                SqlDbType.NVarChar,
                -1,
                SerializeCapabilities(target.Capabilities)));
    }

}