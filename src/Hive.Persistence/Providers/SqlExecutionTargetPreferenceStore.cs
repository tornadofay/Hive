using System.Data;
using Hive.Core;
using Microsoft.Data.SqlClient;

namespace Hive.Persistence;

public sealed class SqlExecutionTargetPreferenceStore : IExecutionTargetPreferenceStore
{
    private readonly HiveDatabaseOptions _options;

    public SqlExecutionTargetPreferenceStore(HiveDatabaseOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<Result<IReadOnlyList<ExecutionTargetId>>> GetFavoriteExecutionTargetIdsAsync(
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateAccessContext(accessContext);
        if (validation is not null)
            return Result<IReadOnlyList<ExecutionTargetId>>.Failure(validation);

        try
        {
            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT [ExecutionTargetId]
                FROM [dbo].[HiveExecutionTargetFavorites]
                WHERE [OwnerPrincipalId] = @OwnerPrincipalId
                  AND [ScopeKind] = @ScopeKind
                  AND [ScopeIdentity] = @ScopeIdentity
                ORDER BY [DisplayOrder] ASC, [ExecutionTargetId] ASC;
                """;

            AddScopeParameters(command, accessContext);

            var ids = new List<ExecutionTargetId>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var ordinal = reader.GetOrdinal("ExecutionTargetId");
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                ids.Add(new ExecutionTargetId(reader.GetGuid(ordinal)));

            return Result<IReadOnlyList<ExecutionTargetId>>.Success(ids);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Result<IReadOnlyList<ExecutionTargetId>>.Failure(
                new Error(
                    "hive.persistence.execution-target-favorites-read-failed",
                    ErrorCategory.External,
                    "Execution target favorites could not be loaded."));
        }
    }

    public async Task<Result<IReadOnlyList<ExecutionTargetId>>> ReplaceFavoriteExecutionTargetIdsAsync(
        IReadOnlyList<ExecutionTargetId> favoriteTargetIds,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(favoriteTargetIds);

        var validation = ValidateAccessContext(accessContext);
        if (validation is not null)
            return Result<IReadOnlyList<ExecutionTargetId>>.Failure(validation);

        try
        {
            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken).ConfigureAwait(false);

            await using (var delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = """
                    DELETE FROM [dbo].[HiveExecutionTargetFavorites]
                    WHERE [OwnerPrincipalId] = @OwnerPrincipalId
                      AND [ScopeKind] = @ScopeKind
                      AND [ScopeIdentity] = @ScopeIdentity;
                    """;
                AddScopeParameters(delete, accessContext);
                await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            for (var index = 0; index < favoriteTargetIds.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = """
                    INSERT INTO [dbo].[HiveExecutionTargetFavorites]
                    (
                        [OwnerPrincipalId],
                        [ScopeKind],
                        [ScopeIdentity],
                        [ExecutionTargetId],
                        [DisplayOrder]
                    )
                    VALUES
                    (
                        @OwnerPrincipalId,
                        @ScopeKind,
                        @ScopeIdentity,
                        @ExecutionTargetId,
                        @DisplayOrder
                    );
                    """;

                AddScopeParameters(insert, accessContext);
                insert.Parameters.Add(
                    new SqlParameter("@ExecutionTargetId", SqlDbType.UniqueIdentifier)
                    {
                        Value = favoriteTargetIds[index].Value
                    });
                insert.Parameters.Add(
                    new SqlParameter("@DisplayOrder", SqlDbType.Int)
                    {
                        Value = index
                    });

                await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return Result<IReadOnlyList<ExecutionTargetId>>.Success(
                favoriteTargetIds.ToArray());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Result<IReadOnlyList<ExecutionTargetId>>.Failure(
                new Error(
                    "hive.persistence.execution-target-favorites-write-failed",
                    ErrorCategory.External,
                    "Execution target favorites could not be saved."));
        }
    }

    private static Error? ValidateAccessContext(ResourceAccessContext? accessContext)
    {
        if (accessContext is null ||
            accessContext.DeploymentId is null ||
            accessContext.PrincipalId is null)
        {
            return Error.Validation(
                "hive.persistence.execution-target-favorites.identity-required",
                "Execution target favorites require a deployment and principal identity.");
        }

        return null;
    }

    private static void AddScopeParameters(
        SqlCommand command,
        ResourceAccessContext accessContext)
    {
        var scope = ResolvePreferenceScope(accessContext);

        command.Parameters.Add(
            new SqlParameter("@OwnerPrincipalId", SqlDbType.UniqueIdentifier)
            {
                Value = accessContext.PrincipalId!.Value.Value
            });
        command.Parameters.Add(
            new SqlParameter("@ScopeKind", SqlDbType.Int)
            {
                Value = (int)scope.Kind
            });
        command.Parameters.Add(
            new SqlParameter("@ScopeIdentity", SqlDbType.UniqueIdentifier)
            {
                Value = scope.Identity
            });
    }

    private static PreferenceScope ResolvePreferenceScope(ResourceAccessContext accessContext)
    {
        if (accessContext.UserId is { } userId &&
            accessContext.TenantId is not null)
        {
            return new PreferenceScope(
                ResourceScopeKind.User,
                userId.Value);
        }

        if (accessContext.TenantId is { } tenantId)
        {
            return new PreferenceScope(
                ResourceScopeKind.Tenant,
                tenantId.Value);
        }

        return new PreferenceScope(
            ResourceScopeKind.Global,
            Guid.Empty);
    }

    private readonly record struct PreferenceScope(
        ResourceScopeKind Kind,
        Guid Identity);
}
