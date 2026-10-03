using System.Collections.ObjectModel;

namespace Hive.Core;

public static class ExecutionTargetFavoriteFilter
{
    public static IReadOnlyList<ExecutionTarget> Apply(
        IReadOnlyList<ExecutionTarget> targets,
        IReadOnlyCollection<ExecutionTargetId>? favoriteTargetIds)
    {
        ArgumentNullException.ThrowIfNull(targets);

        if (favoriteTargetIds is null || favoriteTargetIds.Count == 0)
            return targets;

        var favorites = favoriteTargetIds.ToHashSet();

        return new ReadOnlyCollection<ExecutionTarget>(
            targets
                .Where(target =>
                {
                    ArgumentNullException.ThrowIfNull(target);
                    return favorites.Contains(target.Id);
                })
                .ToList());
    }
}
