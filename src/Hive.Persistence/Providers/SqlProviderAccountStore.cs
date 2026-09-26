using System.Data;
using Hive.Core;
using Microsoft.Data.SqlClient;

namespace Hive.Persistence;

internal sealed class SqlProviderAccountStore : SqlResourceStoreBase
{
    private readonly SqlProviderResourceReader _reader;

    private const string ProviderAccountColumns = """
        [ProviderAccountId],
        [ProviderId],
        [AccountKey],
        [DisplayName],
        [ExternalAccountId],
        [CredentialSecretId],
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

    internal SqlProviderAccountStore(HiveDatabaseOptions options, SqlProviderResourceReader reader)
        : base(options)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    internal Task<Result<ProviderAccount>> CreateProviderAccountAsync(
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

                var provider = await _reader.LoadProviderAsync(
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


    internal Task<Result<ProviderAccount>> GetProviderAccountAsync(
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


    internal Task<Result<IReadOnlyList<ProviderAccount>>> ListProviderAccountsAsync(
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


    internal Task<Result<ProviderAccount>> UpdateProviderAccountAsync(
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

                var current = await _reader.LoadProviderAccountAsync(
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
                    account.ExternalAccountId,
                    account.CredentialSecret);

                await UpdateProviderAccountRowAsync(
                    connection,
                    transaction,
                    current,
                    updated,
                    cancellationToken).ConfigureAwait(false);

                return Result<ProviderAccount>.Success(updated);
            });


    internal Task<Result<ProviderAccount>> DeleteProviderAccountAsync(
        ProviderAccountId providerAccountId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        RetireProviderAccountAsync(
            providerAccountId,
            accessContext,
            cancellationToken);

    internal Task<Result<ProviderAccount>> ReactivateProviderAccountAsync(
        ProviderAccountId providerAccountId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        ReactivateProviderAccountInternalAsync(
            providerAccountId,
            accessContext,
            cancellationToken);


    private Task<Result<ProviderAccount>> ReactivateProviderAccountInternalAsync(
        ProviderAccountId providerAccountId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken) =>
        ExecuteInTransactionAsync(
            "provider account",
            cancellationToken,
            async (connection, transaction) =>
            {
                ValidateAccessContext(accessContext);

                var current = await _reader.LoadProviderAccountAsync(
                    connection,
                    transaction,
                    providerAccountId,
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

                if (current.Resource.Lifecycle.Status == ResourceLifecycleStatus.Active)
                    return Result<ProviderAccount>.Success(current);

                if (current.Resource.Lifecycle.Status != ResourceLifecycleStatus.Retired)
                    return Result<ProviderAccount>.Failure(
                        Conflict(
                            "hive.provider-account.lifecycle-invalid",
                            "Only a retired provider account can be reactivated."));

                var provider = await _reader.LoadProviderAsync(
                    connection,
                    transaction,
                    current.ProviderId,
                    cancellationToken).ConfigureAwait(false);

                if (provider is null)
                    return Result<ProviderAccount>.Failure(
                        NotFound(
                            "hive.provider.not-found",
                            "The provider required by this account no longer exists."));

                var providerAccessError = ValidateAccess(
                    provider.Resource,
                    accessContext,
                    "provider");

                if (providerAccessError is not null)
                    return Result<ProviderAccount>.Failure(providerAccessError);

                if (provider.Resource.Lifecycle.Status != ResourceLifecycleStatus.Active)
                    return Result<ProviderAccount>.Failure(
                        Conflict(
                            "hive.provider-account.provider-inactive",
                            "A provider account cannot be reactivated while its provider is not active."));

                var active = new ProviderAccount(
                    current.Resource.TransitionLifecycle(
                        ResourceLifecycleStatus.Active,
                        DateTimeOffset.UtcNow),
                    current.ProviderId,
                    current.Key,
                    current.DisplayName,
                    current.ExternalAccountId,
                    current.CredentialSecret);

                await UpdateLifecycleAsync(
                    connection,
                    transaction,
                    "HiveProviderAccounts",
                    "ProviderAccountId",
                    active.Id.Value,
                    active.Resource,
                    current.Resource.Version,
                    cancellationToken).ConfigureAwait(false);

                return Result<ProviderAccount>.Success(active);
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

                var current = await _reader.LoadProviderAccountAsync(
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
                    current.ExternalAccountId,
                    current.CredentialSecret);

                await UpdateLifecycleAsync(
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
                @CredentialSecretId,
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
                [CredentialSecretId] = @CredentialSecretId,
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
            GuidParameter(
                "@CredentialSecretId",
                updated.CredentialSecret?.Id.Value));
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
        command.Parameters.Add(
            GuidParameter(
                "@CredentialSecretId",
                account.CredentialSecret?.Id.Value));
    }

}