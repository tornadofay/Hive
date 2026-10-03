using Hive.Core;
using Hive.Persistence;

namespace Hive.Management;

internal sealed class HiveExecutionTargetPreferenceManagementService : HiveManagementServiceBase
{
    private const int MaxFavoriteExecutionTargets = 256;

    private readonly IExecutionTargetPreferenceStore? _preferences;
    private readonly IProviderResourceStore _providerResources;

    internal HiveExecutionTargetPreferenceManagementService(
        IExecutionTargetPreferenceStore? preferences,
        IProviderResourceStore providerResources)
    {
        _preferences = preferences;
        _providerResources = providerResources ?? throw new ArgumentNullException(nameof(providerResources));
    }

    internal Task<Result<IReadOnlyList<ExecutionTargetId>>> GetFavoriteExecutionTargetIdsAsync(
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        if (_preferences is null)
        {
            return Failure(
                Error.Unsupported(
                    "hive.management.execution-target-favorites-unavailable",
                    "Execution target favorites persistence is not configured."));
        }

        return List(
            accessContext,
            "execution target favorites",
            () => _preferences.GetFavoriteExecutionTargetIdsAsync(
                accessContext,
                cancellationToken));
    }

    internal async Task<Result<IReadOnlyList<ExecutionTargetId>>> ReplaceFavoriteExecutionTargetIdsAsync(
        IReadOnlyList<ExecutionTargetId> favoriteTargetIds,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        if (_preferences is null)
        {
            return Result<IReadOnlyList<ExecutionTargetId>>.Failure(
                Error.Unsupported(
                    "hive.management.execution-target-favorites-unavailable",
                    "Execution target favorites persistence is not configured."));
        }

        ArgumentNullException.ThrowIfNull(favoriteTargetIds);

        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return Result<IReadOnlyList<ExecutionTargetId>>.Failure(contextError);

        if (favoriteTargetIds.Count > MaxFavoriteExecutionTargets)
        {
            return Result<IReadOnlyList<ExecutionTargetId>>.Failure(
                Error.Validation(
                    "hive.management.execution-target-favorites.too-many",
                    $"At most {MaxFavoriteExecutionTargets} favorite execution targets may be configured."));
        }

        var ids = new HashSet<ExecutionTargetId>();
        foreach (var targetId in favoriteTargetIds)
        {
            if (targetId == default)
            {
                return Result<IReadOnlyList<ExecutionTargetId>>.Failure(
                    Error.Validation(
                        "hive.management.execution-target-favorites.identity-invalid",
                        "Favorite execution target identities must be non-empty."));
            }

            if (!ids.Add(targetId))
            {
                return Result<IReadOnlyList<ExecutionTargetId>>.Failure(
                    Error.Validation(
                        "hive.management.execution-target-favorites.duplicate",
                        $"Execution target '{targetId}' is listed more than once."));
            }
        }

        var accessibleTargets = await _providerResources
            .ListExecutionTargetsAsync(
                accessContext,
                includeRetired: true,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (accessibleTargets.IsFailure)
            return Result<IReadOnlyList<ExecutionTargetId>>.Failure(accessibleTargets.Error!);

        var accessibleIds = accessibleTargets.Value!
            .Select(target => target.Id)
            .ToHashSet();

        var inaccessible = ids.FirstOrDefault(
            targetId => !accessibleIds.Contains(targetId));

        if (inaccessible != default)
        {
            return Result<IReadOnlyList<ExecutionTargetId>>.Failure(
                new Error(
                    "hive.management.execution-target-favorites.target-forbidden",
                    ErrorCategory.Forbidden,
                    $"Execution target '{inaccessible}' is not accessible in the current resource scope."));
        }

        return await _preferences
            .ReplaceFavoriteExecutionTargetIdsAsync(
                favoriteTargetIds,
                accessContext,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static Task<Result<IReadOnlyList<ExecutionTargetId>>> Failure(
        Error error) =>
        Task.FromResult(
            Result<IReadOnlyList<ExecutionTargetId>>.Failure(error));
}
