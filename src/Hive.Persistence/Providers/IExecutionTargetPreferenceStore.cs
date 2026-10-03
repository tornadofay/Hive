using Hive.Core;

namespace Hive.Persistence;

public interface IExecutionTargetPreferenceStore
{
    Task<Result<IReadOnlyList<ExecutionTargetId>>> GetFavoriteExecutionTargetIdsAsync(
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ExecutionTargetId>>> ReplaceFavoriteExecutionTargetIdsAsync(
        IReadOnlyList<ExecutionTargetId> favoriteTargetIds,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);
}
