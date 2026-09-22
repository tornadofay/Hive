using System.Data;
using System.Text.Json;
using Hive.Core;
using Microsoft.Data.SqlClient;

namespace Hive.Persistence;

public sealed class SqlProviderResourceStore : IProviderResourceStore
{
    private const string ProviderColumns = """
        [ProviderId],
        [ProviderKey],
        [DisplayName],
        [TransportKind],
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

    private const string ProviderAccountColumns = """
        [ProviderAccountId],
        [ProviderId],
        [AccountKey],
        [DisplayName],
        [ExternalAccountId],
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

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);

    private readonly HiveDatabaseOptions _options;

    public SqlProviderResourceStore(HiveDatabaseOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<Result<Provider>> CreateProviderAsync(
        Provider provider,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(
            "provider",
            cancellationToken,
            async (connection, transaction) =>
            {
                var validation = ValidateCreate(
                    provider.Resource,
                    ResourceKind.Provider,
                    accessContext,
                    "provider");

                if (validation is not null)
                    return Result<Provider>.Failure(validation);

                await InsertProviderAsync(
                    connection,
                    transaction,
                    provider,
                    cancellationToken).ConfigureAwait(false);

                return Result<Provider>.Success(provider);
            });

    public Task<Result<Provider>> GetProviderAsync(
        ProviderId providerId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            "provider",
            cancellationToken,
            async connection =>
            {
                ValidateAccessContext(accessContext);

                await using var command = CreateCommand(
                    connection,
                    $"""
                    SELECT {ProviderColumns}
                    FROM [dbo].[HiveProviders]
                    WHERE [ProviderId] = @ProviderId;
                    """);

                command.Parameters.Add(GuidParameter("@ProviderId", providerId.Value));

                await using var reader = await command.ExecuteReaderAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return Result<Provider>.Failure(
                        NotFound(
                            "hive.provider.not-found",
                            "The requested provider does not exist."));
                }

                var provider = ReadProvider(reader);
                var accessError = ValidateAccess(
                    provider.Resource,
                    accessContext,
                    "provider");

                return accessError is null
                    ? Result<Provider>.Success(provider)
                    : Result<Provider>.Failure(accessError);
            });

    public Task<Result<IReadOnlyList<Provider>>> ListProvidersAsync(
        ResourceAccessContext accessContext,
        bool includeRetired = false,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            "provider",
            cancellationToken,
            async connection =>
            {
                ValidateAccessContext(accessContext);

                await using var command = CreateCommand(
                    connection,
                    $"""
                    SELECT {ProviderColumns}
                    FROM [dbo].[HiveProviders]
                    WHERE [OwnerPrincipalId] = @PrincipalId
                      AND {ScopeAccessPredicate}
                    {(includeRetired
                        ? string.Empty
                        : "AND [LifecycleStatus] <> @RetiredLifecycle")}
                    ORDER BY [DisplayName], [ProviderId];
                    """);

                AddAccessParameters(command, accessContext);

                if (!includeRetired)
                {
                    command.Parameters.Add(
                        IntParameter(
                            "@RetiredLifecycle",
                            (int)ResourceLifecycleStatus.Retired));
                }

                var items = new List<Provider>();

                await using var reader = await command.ExecuteReaderAsync(cancellationToken)
                    .ConfigureAwait(false);

                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    items.Add(ReadProvider(reader));

                return Result<IReadOnlyList<Provider>>.Success(items);
            });

    public Task<Result<Provider>> UpdateProviderAsync(
        Provider provider,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(
            "provider",
            cancellationToken,
            async (connection, transaction) =>
            {
                ValidateAccessContext(accessContext);

                var validation = ValidateUpdate(
                    provider.Resource,
                    ResourceKind.Provider,
                    accessContext,
                    "provider");

                if (validation is not null)
                    return Result<Provider>.Failure(validation);

                var current = await LoadProviderAsync(
                    connection,
                    transaction,
                    provider.Id,
                    cancellationToken).ConfigureAwait(false);

                if (current is null)
                    return Result<Provider>.Failure(
                        NotFound(
                            "hive.provider.not-found",
                            "The requested provider does not exist."));

                var accessError = ValidateAccess(
                    current.Resource,
                    accessContext,
                    "provider");

                if (accessError is not null)
                    return Result<Provider>.Failure(accessError);

                if (provider.Resource.Version != current.Resource.Version)
                {
                    return Result<Provider>.Failure(
                        Concurrency(
                            "hive.provider.concurrency",
                            "The provider version is stale."));
                }

                if (current.Resource.Lifecycle.Status == ResourceLifecycleStatus.Retired)
                {
                    return Result<Provider>.Failure(
                        Conflict(
                            "hive.provider.retired",
                            "A retired provider cannot be updated."));
                }

                if (!string.Equals(
                    provider.Key,
                    current.Key,
                    StringComparison.Ordinal))
                {
                    return Result<Provider>.Failure(
                        Conflict(
                            "hive.provider.key-immutable",
                            "Provider key cannot be changed after creation."));
                }

                var updated = new Provider(
                    new ResourceEnvelope<ProviderId>(
                        ResourceKind.Provider,
                        current.Id,
                        current.Resource.Owner,
                        current.Resource.Scope,
                        current.Resource.Version.Next(),
                        current.Resource.Provenance,
                        current.Resource.Lifecycle,
                        provider.Resource.Metadata),
                    current.Key,
                    provider.DisplayName,
                    provider.TransportKind);

                await UpdateProviderRowAsync(
                    connection,
                    transaction,
                    current,
                    updated,
                    cancellationToken).ConfigureAwait(false);

                return Result<Provider>.Success(updated);
            });

    public Task<Result<Provider>> DeleteProviderAsync(
        ProviderId providerId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        RetireProviderAsync(providerId, accessContext, cancellationToken);

    public Task<Result<ProviderAccount>> CreateProviderAccountAsync(
        ProviderAccount account,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(
            "provider account",
            cancellationToken,
            async (connection, transaction) =>
            {
                var validation = ValidateCreate(
                    account.Resource,
                    ResourceKind.ProviderAccount,
                    accessContext,
                    "provider account");

                if (validation is not null)
                    return Result<ProviderAccount>.Failure(validation);

                var provider = await LoadProviderAsync(
                    connection,
                    transaction,
                    account.ProviderId,
                    cancellationToken).ConfigureAwait(false);

                if (provider is null)
                {
                    return Result<ProviderAccount>.Failure(
                        NotFound(
                            "hive.provider.not-found",
                            "The requested provider does not exist."));
                }

                var providerAccessError = ValidateAccess(
                    provider.Resource,
                    accessContext,
                    "provider");

                if (providerAccessError is not null)
                    return Result<ProviderAccount>.Failure(providerAccessError);

                if (provider.Resource.Lifecycle.Status == ResourceLifecycleStatus.Retired)
                {
                    return Result<ProviderAccount>.Failure(
                        Conflict(
                            "hive.provider-account.provider-retired",
                            "A provider account cannot be created under a retired provider."));
                }

                await InsertProviderAccountAsync(
                    connection,
                    transaction,
                    account,
                    cancellationToken).ConfigureAwait(false);

                return Result<ProviderAccount>.Success(account);
            });

    public Task<Result<ProviderAccount>> GetProviderAccountAsync(
        ProviderAccountId providerAccountId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            "provider account",
            cancellationToken,
            async connection =>
            {
                ValidateAccessContext(accessContext);

                await using var command = CreateCommand(
                    connection,
                    $"""
                    SELECT {ProviderAccountColumns}
                    FROM [dbo].[HiveProviderAccounts]
                    WHERE [ProviderAccountId] = @ProviderAccountId;
                    """);

                command.Parameters.Add(
                    GuidParameter(
                        "@ProviderAccountId",
                        providerAccountId.Value));

                await using var reader = await command.ExecuteReaderAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return Result<ProviderAccount>.Failure(
                        NotFound(
                            "hive.provider-account.not-found",
                            "The requested provider account does not exist."));
                }

                var account = ReadProviderAccount(reader);
                var accessError = ValidateAccess(
                    account.Resource,
                    accessContext,
                    "provider account");

                return accessError is null
                    ? Result<ProviderAccount>.Success(account)
                    : Result<ProviderAccount>.Failure(accessError);
            });

    public Task<Result<IReadOnlyList<ProviderAccount>>> ListProviderAccountsAsync(
        ProviderId providerId,
        ResourceAccessContext accessContext,
        bool includeRetired = false,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            "provider account",
            cancellationToken,
            async connection =>
            {
                ValidateAccessContext(accessContext);

                await using var command = CreateCommand(
                    connection,
                    $"""
                    SELECT {ProviderAccountColumns}
                    FROM [dbo].[HiveProviderAccounts]
                    WHERE [ProviderId] = @ProviderId
                      AND [OwnerPrincipalId] = @PrincipalId
                      AND {ScopeAccessPredicate}
                    {(includeRetired
                        ? string.Empty
                        : "AND [LifecycleStatus] <> @RetiredLifecycle")}
                    ORDER BY [DisplayName], [ProviderAccountId];
                    """);

                command.Parameters.Add(GuidParameter("@ProviderId", providerId.Value));
                AddAccessParameters(command, accessContext);

                if (!includeRetired)
                {
                    command.Parameters.Add(
                        IntParameter(
                            "@RetiredLifecycle",
                            (int)ResourceLifecycleStatus.Retired));
                }

                var items = new List<ProviderAccount>();

                await using var reader = await command.ExecuteReaderAsync(cancellationToken)
                    .ConfigureAwait(false);

                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    items.Add(ReadProviderAccount(reader));

                return Result<IReadOnlyList<ProviderAccount>>.Success(items);
            });

    public Task<Result<ProviderAccount>> UpdateProviderAccountAsync(
        ProviderAccount account,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(
            "provider account",
            cancellationToken,
            async (connection, transaction) =>
            {
                ValidateAccessContext(accessContext);

                var validation = ValidateUpdate(
                    account.Resource,
                    ResourceKind.ProviderAccount,
                    accessContext,
                    "provider account");

                if (validation is not null)
                    return Result<ProviderAccount>.Failure(validation);

                var current = await LoadProviderAccountAsync(
                    connection,
                    transaction,
                    account.Id,
                    cancellationToken).ConfigureAwait(false);

                if (current is null)
                    return Result<ProviderAccount>.Failure(
                        NotFound(
                            "hive.provider-account.not-found",
                            "The requested provider account does not exist."));

                var accessError = ValidateAccess(
                    current.Resource,
                    accessContext,
                    "provider account");

                if (accessError is not null)
                    return Result<ProviderAccount>.Failure(accessError);

                if (account.Resource.Version != current.Resource.Version)
                {
                    return Result<ProviderAccount>.Failure(
                        Concurrency(
                            "hive.provider-account.concurrency",
                            "The provider account version is stale."));
                }

                if (current.Resource.Lifecycle.Status == ResourceLifecycleStatus.Retired)
                {
                    return Result<ProviderAccount>.Failure(
                        Conflict(
                            "hive.provider-account.retired",
                            "A retired provider account cannot be updated."));
                }

                if (account.ProviderId != current.ProviderId)
                {
                    return Result<ProviderAccount>.Failure(
                        Conflict(
                            "hive.provider-account.provider-immutable",
                            "Provider identity cannot be changed after creation."));
                }

                if (!string.Equals(
                    account.Key,
                    current.Key,
                    StringComparison.Ordinal))
                {
                    return Result<ProviderAccount>.Failure(
                        Conflict(
                            "hive.provider-account.key-immutable",
                            "Provider account key cannot be changed after creation."));
                }

                var updated = new ProviderAccount(
                    new ResourceEnvelope<ProviderAccountId>(
                        ResourceKind.ProviderAccount,
                        current.Id,
                        current.Resource.Owner,
                        current.Resource.Scope,
                        current.Resource.Version.Next(),
                        current.Resource.Provenance,
                        current.Resource.Lifecycle,
                        account.Resource.Metadata),
                    current.ProviderId,
                    current.Key,
                    account.DisplayName,
                    account.ExternalAccountId);

                await UpdateProviderAccountRowAsync(
                    connection,
                    transaction,
                    current,
                    updated,
                    cancellationToken).ConfigureAwait(false);

                return Result<ProviderAccount>.Success(updated);
            });

    public Task<Result<ProviderAccount>> DeleteProviderAccountAsync(
        ProviderAccountId providerAccountId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        RetireProviderAccountAsync(
            providerAccountId,
            accessContext,
            cancellationToken);

    public Task<Result<ExecutionTarget>> CreateExecutionTargetAsync(
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

                var provider = await LoadProviderAsync(
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

                var account = await LoadProviderAccountAsync(
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

    public Task<Result<ExecutionTarget>> GetExecutionTargetAsync(
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

    public Task<Result<IReadOnlyList<ExecutionTarget>>> ListExecutionTargetsAsync(
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

    public Task<Result<ExecutionTarget>> UpdateExecutionTargetAsync(
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

                var current = await LoadExecutionTargetAsync(
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

    public Task<Result<ExecutionTarget>> DeleteExecutionTargetAsync(
        ExecutionTargetId executionTargetId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        RetireExecutionTargetAsync(
            executionTargetId,
            accessContext,
            cancellationToken);

    private Task<Result<Provider>> RetireProviderAsync(
        ProviderId providerId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken) =>
        ExecuteInTransactionAsync(
            "provider",
            cancellationToken,
            async (connection, transaction) =>
            {
                ValidateAccessContext(accessContext);

                var current = await LoadProviderAsync(
                    connection,
                    transaction,
                    providerId,
                    cancellationToken).ConfigureAwait(false);

                if (current is null)
                {
                    return Result<Provider>.Failure(
                        NotFound(
                            "hive.provider.not-found",
                            "The requested provider does not exist."));
                }

                var accessError = ValidateAccess(
                    current.Resource,
                    accessContext,
                    "provider");

                if (accessError is not null)
                    return Result<Provider>.Failure(accessError);

                if (current.Resource.Lifecycle.Status == ResourceLifecycleStatus.Retired)
                    return Result<Provider>.Success(current);

                var retired = new Provider(
                    current.Resource.TransitionLifecycle(
                        ResourceLifecycleStatus.Retired,
                        DateTimeOffset.UtcNow),
                    current.Key,
                    current.DisplayName,
                    current.TransportKind);

                await UpdateRetiredAsync(
                    connection,
                    transaction,
                    "HiveProviders",
                    "ProviderId",
                    retired.Id.Value,
                    retired.Resource,
                    current.Resource.Version,
                    cancellationToken).ConfigureAwait(false);

                return Result<Provider>.Success(retired);
            });

    private Task<Result<ProviderAccount>> RetireProviderAccountAsync(
        ProviderAccountId providerAccountId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken) =>
        ExecuteInTransactionAsync(
            "provider account",
            cancellationToken,
            async (connection, transaction) =>
            {
                ValidateAccessContext(accessContext);

                var current = await LoadProviderAccountAsync(
                    connection,
                    transaction,
                    providerAccountId,
                    cancellationToken).ConfigureAwait(false);

                if (current is null)
                {
                    return Result<ProviderAccount>.Failure(
                        NotFound(
                            "hive.provider-account.not-found",
                            "The requested provider account does not exist."));
                }

                var accessError = ValidateAccess(
                    current.Resource,
                    accessContext,
                    "provider account");

                if (accessError is not null)
                    return Result<ProviderAccount>.Failure(accessError);

                if (current.Resource.Lifecycle.Status == ResourceLifecycleStatus.Retired)
                    return Result<ProviderAccount>.Success(current);

                var retired = new ProviderAccount(
                    current.Resource.TransitionLifecycle(
                        ResourceLifecycleStatus.Retired,
                        DateTimeOffset.UtcNow),
                    current.ProviderId,
                    current.Key,
                    current.DisplayName,
                    current.ExternalAccountId);

                await UpdateRetiredAsync(
                    connection,
                    transaction,
                    "HiveProviderAccounts",
                    "ProviderAccountId",
                    retired.Id.Value,
                    retired.Resource,
                    current.Resource.Version,
                    cancellationToken).ConfigureAwait(false);

                return Result<ProviderAccount>.Success(retired);
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

                var current = await LoadExecutionTargetAsync(
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

                await UpdateRetiredAsync(
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

    private async Task InsertProviderAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Provider provider,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            $"""
            INSERT INTO [dbo].[HiveProviders]
            (
                {ProviderColumns}
            )
            VALUES
            (
                @ProviderId,
                @ProviderKey,
                @DisplayName,
                @TransportKind,
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

        AddProviderParameters(command, provider);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task InsertProviderAccountAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ProviderAccount account,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            $"""
            INSERT INTO [dbo].[HiveProviderAccounts]
            (
                {ProviderAccountColumns}
            )
            VALUES
            (
                @ProviderAccountId,
                @ProviderId,
                @AccountKey,
                @DisplayName,
                @ExternalAccountId,
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

        AddProviderAccountParameters(command, account);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

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

    private async Task UpdateProviderRowAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Provider current,
        Provider updated,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            """
            UPDATE [dbo].[HiveProviders]
            SET [DisplayName] = @DisplayName,
                [TransportKind] = @TransportKind,
                [ResourceVersion] = @NewVersion,
                [MetadataJson] = @MetadataJson
            WHERE [ProviderId] = @ProviderId
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
                "@TransportKind",
                SqlDbType.NVarChar,
                100,
                updated.TransportKind));
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
            GuidParameter("@ProviderId", updated.Id.Value));
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

    private async Task UpdateProviderAccountRowAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ProviderAccount current,
        ProviderAccount updated,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            """
            UPDATE [dbo].[HiveProviderAccounts]
            SET [DisplayName] = @DisplayName,
                [ExternalAccountId] = @ExternalAccountId,
                [ResourceVersion] = @NewVersion,
                [MetadataJson] = @MetadataJson
            WHERE [ProviderAccountId] = @ProviderAccountId
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
                "@ExternalAccountId",
                SqlDbType.NVarChar,
                200,
                updated.ExternalAccountId));
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
                "@ProviderAccountId",
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

    private async Task UpdateRetiredAsync<TIdentity>(
        SqlConnection connection,
        SqlTransaction transaction,
        string tableName,
        string identityColumn,
        Guid resourceId,
        ResourceEnvelope<TIdentity> retiredResource,
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
                retiredResource.Version.Value));
        command.Parameters.Add(
            IntParameter(
                "@LifecycleStatus",
                (int)retiredResource.Lifecycle.Status));
        command.Parameters.Add(
            DateTimeParameter(
                "@LifecycleChangedAtUtc",
                retiredResource.Lifecycle.ChangedAtUtc));
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

    private async Task<Provider?> LoadProviderAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ProviderId providerId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            $"""
            SELECT {ProviderColumns}
            FROM [dbo].[HiveProviders] WITH (UPDLOCK, HOLDLOCK)
            WHERE [ProviderId] = @ProviderId;
            """,
            transaction);

        command.Parameters.Add(GuidParameter("@ProviderId", providerId.Value));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);

        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadProvider(reader)
            : null;
    }

    private async Task<ProviderAccount?> LoadProviderAccountAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ProviderAccountId providerAccountId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            $"""
            SELECT {ProviderAccountColumns}
            FROM [dbo].[HiveProviderAccounts] WITH (UPDLOCK, HOLDLOCK)
            WHERE [ProviderAccountId] = @ProviderAccountId;
            """,
            transaction);

        command.Parameters.Add(
            GuidParameter(
                "@ProviderAccountId",
                providerAccountId.Value));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);

        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadProviderAccount(reader)
            : null;
    }

    private async Task<ExecutionTarget?> LoadExecutionTargetAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ExecutionTargetId executionTargetId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            $"""
            SELECT {ExecutionTargetColumns}
            FROM [dbo].[HiveExecutionTargets] WITH (UPDLOCK, HOLDLOCK)
            WHERE [ExecutionTargetId] = @ExecutionTargetId;
            """,
            transaction);

        command.Parameters.Add(
            GuidParameter(
                "@ExecutionTargetId",
                executionTargetId.Value));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);

        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadExecutionTarget(reader)
            : null;
    }

    private static Provider ReadProvider(SqlDataReader reader) =>
        new(
            ReadResourceEnvelope<ProviderId>(
                reader,
                ResourceKind.Provider,
                "ProviderId",
                static value => new ProviderId(value)),
            reader.GetString(reader.GetOrdinal("ProviderKey")),
            reader.GetString(reader.GetOrdinal("DisplayName")),
            reader.GetString(reader.GetOrdinal("TransportKind")));

    private static ProviderAccount ReadProviderAccount(SqlDataReader reader) =>
        new(
            ReadResourceEnvelope<ProviderAccountId>(
                reader,
                ResourceKind.ProviderAccount,
                "ProviderAccountId",
                static value => new ProviderAccountId(value)),
            new ProviderId(reader.GetGuid(reader.GetOrdinal("ProviderId"))),
            reader.GetString(reader.GetOrdinal("AccountKey")),
            reader.GetString(reader.GetOrdinal("DisplayName")),
            reader.IsDBNull(reader.GetOrdinal("ExternalAccountId"))
                ? null
                : reader.GetString(reader.GetOrdinal("ExternalAccountId")));

    private static ExecutionTarget ReadExecutionTarget(SqlDataReader reader) =>
        new(
            ReadResourceEnvelope<ExecutionTargetId>(
                reader,
                ResourceKind.ExecutionTarget,
                "ExecutionTargetId",
                static value => new ExecutionTargetId(value)),
            new ProviderId(reader.GetGuid(reader.GetOrdinal("ProviderId"))),
            new ProviderAccountId(reader.GetGuid(reader.GetOrdinal("ProviderAccountId"))),
            reader.GetString(reader.GetOrdinal("TargetKey")),
            reader.GetString(reader.GetOrdinal("DisplayName")),
            new Uri(
                reader.GetString(reader.GetOrdinal("EndpointUri")),
                UriKind.Absolute),
            reader.IsDBNull(reader.GetOrdinal("Model"))
                ? null
                : reader.GetString(reader.GetOrdinal("Model")),
            reader.IsDBNull(reader.GetOrdinal("Deployment"))
                ? null
                : reader.GetString(reader.GetOrdinal("Deployment")),
            DeserializeCapabilities(
                reader.GetString(reader.GetOrdinal("CapabilitiesJson"))));

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

    private static IReadOnlyDictionary<string, string> DeserializeMetadata(
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

    private static IReadOnlyList<CapabilityStateEntry> DeserializeCapabilities(
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

    private static string SerializeMetadata(
        IReadOnlyDictionary<string, string> metadata) =>
        JsonSerializer.Serialize(metadata, JsonOptions);

    private static string SerializeCapabilities(
        IReadOnlyList<CapabilityStateEntry> capabilities) =>
        JsonSerializer.Serialize(
            capabilities.Select(
                capability => new CapabilityPersistenceItem(
                    capability.Capability.Value,
                    capability.State)),
            JsonOptions);

    private static Error? ValidateCreate<TIdentity>(
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

    private static Error? ValidateUpdate<TIdentity>(
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

    private static Error? ValidateAccess<TIdentity>(
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

    private static void ValidateAccessContext(
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

    private static void AddProviderParameters(
        SqlCommand command,
        Provider provider)
    {
        AddResourceParameters(command, provider.Resource);
        command.Parameters.Add(
            GuidParameter("@ProviderId", provider.Id.Value));
        command.Parameters.Add(
            SqlParameter(
                "@ProviderKey",
                SqlDbType.NVarChar,
                100,
                provider.Key));
        command.Parameters.Add(
            SqlParameter(
                "@DisplayName",
                SqlDbType.NVarChar,
                200,
                provider.DisplayName));
        command.Parameters.Add(
            SqlParameter(
                "@TransportKind",
                SqlDbType.NVarChar,
                100,
                provider.TransportKind));
    }

    private static void AddProviderAccountParameters(
        SqlCommand command,
        ProviderAccount account)
    {
        AddResourceParameters(command, account.Resource);
        command.Parameters.Add(
            GuidParameter(
                "@ProviderAccountId",
                account.Id.Value));
        command.Parameters.Add(
            GuidParameter(
                "@ProviderId",
                account.ProviderId.Value));
        command.Parameters.Add(
            SqlParameter(
                "@AccountKey",
                SqlDbType.NVarChar,
                100,
                account.Key));
        command.Parameters.Add(
            SqlParameter(
                "@DisplayName",
                SqlDbType.NVarChar,
                200,
                account.DisplayName));
        command.Parameters.Add(
            SqlParameter(
                "@ExternalAccountId",
                SqlDbType.NVarChar,
                200,
                account.ExternalAccountId));
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

    private static void AddResourceParameters<TIdentity>(
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

    private static SqlParameter SqlParameter(
        string name,
        SqlDbType type,
        object? value) =>
        new(name, type)
        {
            Value = value ?? DBNull.Value
        };

    private static void AddAccessParameters(
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

    private static Error NotFound(string code, string message) =>
        new(code, ErrorCategory.NotFound, message);

    private static Error Conflict(string code, string message) =>
        new(code, ErrorCategory.Conflict, message);

    private static Error Forbidden(string code, string message) =>
        new(code, ErrorCategory.Forbidden, message);

    private static Error Concurrency(string code, string message) =>
        new(code, ErrorCategory.Concurrency, message);

    private static Error ToSqlError(
        string resourceName,
        SqlException exception) =>
        new(
            $"hive.persistence.{resourceName.Replace(' ', '-')}.sql-failure",
            ErrorCategory.External,
            $"SQL Server operation for the {resourceName} failed: {exception.Message}");

    private static Error ToInvalidStateError(
        string resourceName,
        Exception exception) =>
        new(
            $"hive.persistence.{resourceName.Replace(' ', '-')}.invalid-state",
            ErrorCategory.Internal,
            $"Persisted {resourceName} state could not be read or validated: {exception.Message}");

    private sealed record CapabilityPersistenceItem(
        string Key,
        CapabilityState State);

    private sealed class ConcurrencyException : Exception
    {
    }

    private static bool IsConstraintConflict(SqlException exception) =>
        exception.Number is 2601 or 2627;
}
