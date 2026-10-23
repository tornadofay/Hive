using Hive.Core;
using Hive.Management;

namespace Hive.Host.WinForms;

internal enum DirectLlmTargetSource
{
    AllEligible,
    Favorites
}

internal sealed record DirectLlmTargetOption(ExecutionTarget Target, string DisplayLabel)
{
    public override string ToString() => DisplayLabel;
}

internal sealed record DirectLlmTargetCatalogResult(
    IReadOnlyList<DirectLlmTargetOption> Targets,
    DirectLlmTargetSource Source,
    int FavoriteCount)
{
    internal string DescribeStatus()
    {
        if (Source == DirectLlmTargetSource.Favorites)
        {
            return Targets.Count == 0
                ? $"None of your {FavoriteCount} favorite ExecutionTarget(s) is currently usable. Update favorites in Provider Settings."
                : $"{Targets.Count} favorite ExecutionTarget(s) available.";
        }

        return Targets.Count == 0
            ? "No active OpenAI-compatible ExecutionTargets are available. Configure one in Settings first."
            : $"{Targets.Count} active ExecutionTarget(s) available (no favorites set).";
    }
}

/// <summary>
/// Builds the ExecutionTarget list for Direct LLM chat. It keeps the existing eligibility checks and then
/// applies the saved favorite-target preference through <see cref="ExecutionTargetFavoriteFilter"/>.
/// No favorites means all eligible targets. Favorites set means only those favorites that are eligible,
/// and the list never falls back to non-favorites. A favorites load failure fails closed.
/// </summary>
internal static class DirectLlmTargetCatalog
{
    internal const string FavoritesUnavailableErrorCode =
        "hive.management.execution-target-favorites-unavailable";

    internal static async Task<Result<DirectLlmTargetCatalogResult>> LoadAsync(
        IHiveManagementFacade management,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(management);
        ArgumentNullException.ThrowIfNull(accessContext);

        var eligible = await LoadEligibleAsync(
            management,
            accessContext,
            cancellationToken).ConfigureAwait(false);

        if (eligible.IsFailure)
            return Result<DirectLlmTargetCatalogResult>.Failure(eligible.Error!);

        var favorites = await management.GetFavoriteExecutionTargetIdsAsync(
            accessContext,
            cancellationToken).ConfigureAwait(false);

        IReadOnlyList<ExecutionTargetId> favoriteIds;
        if (favorites.IsFailure)
        {
            // A store that is not configured means favorites do not exist for this host.
            // Any other failure must not silently broaden the list.
            if (!string.Equals(
                    favorites.Error!.Code,
                    FavoritesUnavailableErrorCode,
                    StringComparison.Ordinal))
            {
                return Result<DirectLlmTargetCatalogResult>.Failure(favorites.Error!);
            }

            favoriteIds = Array.Empty<ExecutionTargetId>();
        }
        else
        {
            favoriteIds = favorites.Value!;
        }

        return Result<DirectLlmTargetCatalogResult>.Success(
            Apply(eligible.Value!, favoriteIds));
    }

    internal static DirectLlmTargetCatalogResult Apply(
        IReadOnlyList<DirectLlmTargetOption> eligible,
        IReadOnlyList<ExecutionTargetId> favoriteIds)
    {
        ArgumentNullException.ThrowIfNull(eligible);
        ArgumentNullException.ThrowIfNull(favoriteIds);

        var qualifying = ExecutionTargetFavoriteFilter.Apply(
            eligible.Select(static option => option.Target).ToList(),
            favoriteIds);

        var qualifyingIds = qualifying
            .Select(static target => target.Id)
            .ToHashSet();

        var targets = eligible
            .Where(option => qualifyingIds.Contains(option.Target.Id))
            .OrderBy(static option => option.DisplayLabel, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new DirectLlmTargetCatalogResult(
            targets,
            favoriteIds.Count == 0
                ? DirectLlmTargetSource.AllEligible
                : DirectLlmTargetSource.Favorites,
            favoriteIds.Count);
    }

    private static async Task<Result<IReadOnlyList<DirectLlmTargetOption>>> LoadEligibleAsync(
        IHiveManagementFacade management,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken)
    {
        var providers = await management.ListProvidersAsync(
            accessContext,
            includeRetired: false,
            cancellationToken).ConfigureAwait(false);

        if (providers.IsFailure)
            return Result<IReadOnlyList<DirectLlmTargetOption>>.Failure(providers.Error!);

        var options = new List<DirectLlmTargetOption>();

        foreach (var provider in providers.Value!)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsEligibleProvider(provider))
                continue;

            var accounts = await management.ListProviderAccountsAsync(
                provider.Id,
                accessContext,
                includeRetired: false,
                cancellationToken).ConfigureAwait(false);

            if (accounts.IsFailure)
                return Result<IReadOnlyList<DirectLlmTargetOption>>.Failure(accounts.Error!);

            foreach (var account in accounts.Value!)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (account.Resource.Lifecycle.Status != ResourceLifecycleStatus.Active)
                    continue;

                var targets = await management.ListExecutionTargetsAsync(
                    account.Id,
                    accessContext,
                    includeRetired: false,
                    cancellationToken).ConfigureAwait(false);

                if (targets.IsFailure)
                    return Result<IReadOnlyList<DirectLlmTargetOption>>.Failure(targets.Error!);

                foreach (var target in targets.Value!)
                {
                    if (!IsEligibleTarget(target))
                        continue;

                    options.Add(new DirectLlmTargetOption(
                        target,
                        $"{provider.DisplayName} / {account.DisplayName} / " +
                        $"{target.DisplayName} ({target.Model ?? target.Deployment})"));
                }
            }
        }

        return Result<IReadOnlyList<DirectLlmTargetOption>>.Success(options);
    }

    private static bool IsEligibleProvider(Provider provider) =>
        provider.Resource.Lifecycle.Status == ResourceLifecycleStatus.Active &&
        BuiltInProviderCatalog.Find(provider.Key)?.RequiresNativeIntegration != true &&
        string.Equals(
            provider.TransportKind,
            "openai-compatible",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsEligibleTarget(ExecutionTarget target) =>
        target.Resource.Lifecycle.Status == ResourceLifecycleStatus.Active &&
        !target.Capabilities.Any(static capability =>
            capability.Capability == HiveCapabilityKeys.TextGeneration &&
            capability.State == CapabilityState.Unsupported);
}
