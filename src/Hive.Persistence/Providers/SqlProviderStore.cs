using System.Data;
using Hive.Core;
using Microsoft.Data.SqlClient;

namespace Hive.Persistence;

internal sealed class SqlProviderStore : SqlResourceStoreBase
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

    internal SqlProviderStore(HiveDatabaseOptions options)
        : base(options)
    {
    }

    internal Task<Result<Provider>> CreateProviderAsync(
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


    internal Task<Result<Provider>> GetProviderAsync(
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


    internal Task<Result<IReadOnlyList<Provider>>> ListProvidersAsync(
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


    internal Task<Result<Provider>> UpdateProviderAsync(
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


    internal Task<Result<Provider>> DeleteProviderAsync(
        ProviderId providerId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        RetireProviderAsync(providerId, accessContext, cancellationToken);

    internal Task<Result<Provider>> ReactivateProviderAsync(
        ProviderId providerId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        ReactivateProviderInternalAsync(providerId, accessContext, cancellationToken);


    private Task<Result<Provider>> ReactivateProviderInternalAsync(
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

                if (current.Resource.Lifecycle.Status == ResourceLifecycleStatus.Active)
                    return Result<Provider>.Success(current);

                if (current.Resource.Lifecycle.Status != ResourceLifecycleStatus.Retired)
                    return Result<Provider>.Failure(
                        Conflict(
                            "hive.provider.lifecycle-invalid",
                            "Only a retired provider can be reactivated."));

                var active = new Provider(
                    current.Resource.TransitionLifecycle(
                        ResourceLifecycleStatus.Active,
                        DateTimeOffset.UtcNow),
                    current.Key,
                    current.DisplayName,
                    current.TransportKind);

                await UpdateLifecycleAsync(
                    connection,
                    transaction,
                    "HiveProviders",
                    "ProviderId",
                    active.Id.Value,
                    active.Resource,
                    current.Resource.Version,
                    cancellationToken).ConfigureAwait(false);

                return Result<Provider>.Success(active);
            });


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

                await UpdateLifecycleAsync(
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

}