using System.Data;
using Hive.Core;
using Microsoft.Data.SqlClient;

namespace Hive.Persistence;

internal sealed class SqlProviderResourceReader : SqlResourceStoreBase
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

    internal SqlProviderResourceReader(HiveDatabaseOptions options)
        : base(options)
    {
    }

    internal async Task<Provider?> LoadProviderAsync(
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


    internal async Task<ProviderAccount?> LoadProviderAccountAsync(
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
                : reader.GetString(reader.GetOrdinal("ExternalAccountId")),
            reader.IsDBNull(reader.GetOrdinal("CredentialSecretId"))
                ? null
                : new SecretReference(
                    new SecretId(
                        reader.GetGuid(reader.GetOrdinal("CredentialSecretId")))));


    internal async Task<ExecutionTarget?> LoadExecutionTargetAsync(
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

}