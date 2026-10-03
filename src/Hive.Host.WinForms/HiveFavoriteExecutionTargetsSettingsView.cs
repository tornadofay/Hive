using System.Drawing;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Host.WinForms;

internal sealed class HiveFavoriteExecutionTargetsSettingsView : UserControl
{
    private readonly IHiveManagementFacade _management;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveThemeManager _themeManager;
    private readonly IHiveExampleOutput? _output;
    private readonly HiveCrudPage<FavoriteExecutionTargetRow> _page;

    public HiveFavoriteExecutionTargetsSettingsView(
        IHiveManagementFacade management,
        ResourceAccessContext accessContext,
        IHiveThemeManager themeManager,
        IHiveExampleOutput? output = null)
    {
        _management = management ?? throw new ArgumentNullException(nameof(management));
        _accessContext = accessContext ?? throw new ArgumentNullException(nameof(accessContext));
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _output = output;

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;

        _page = new HiveCrudPage<FavoriteExecutionTargetRow>
        {
            Title = "Favorite Execution Targets",
            Description = "Manage the execution targets you have saved as favorites.",
            PageSize = 20,
            AllowAdd = true,
            AllowEdit = false,
            AllowDelete = true,
            ShowRefresh = true,
            ShowSearch = true,
            SearchPlaceholder = "Search favorite execution targets..."
        };
        _page.AddButtonText = "Add Favorite";

        _page.SetColumns(
            new HiveCrudColumn<FavoriteExecutionTargetRow>(
                "Execution Target",
                260,
                item => item.DisplayName),
            new HiveCrudColumn<FavoriteExecutionTargetRow>(
                "Provider",
                190,
                item => item.ProviderName),
            new HiveCrudColumn<FavoriteExecutionTargetRow>(
                "Account",
                190,
                item => item.AccountName),
            new HiveCrudColumn<FavoriteExecutionTargetRow>(
                "Model / Deployment",
                230,
                item => item.ModelDeployment),
            new HiveCrudColumn<FavoriteExecutionTargetRow>(
                "Status",
                110,
                item => item.Status,
                item => item.Status == "Active"
                    ? _themeManager.Theme.VisualStates.Success
                    : item.Status == "Unavailable"
                        ? _themeManager.Theme.VisualStates.Error
                        : _themeManager.Theme.VisualStates.Warning));

        _page.LoadItemsAsync = LoadAsync;
        _page.EditItemAsync = AddFavoriteAsync;
        _page.DeleteItemAsync = RemoveFavoriteAsync;
        _page.CanDeleteItem = _ => true;
        _page.GetItemDisplayName = item => item.DisplayName;
        _page.OperationFailed += PageOperationFailed;

        Controls.Add(_page);
        _themeManager.Apply(this);
    }

    internal HiveCrudPage<FavoriteExecutionTargetRow> CrudPage => _page;

    internal ListView FavoriteList => _page.ListView;

    public Task InitializeAsync(
        CancellationToken cancellationToken = default) =>
        _page.RefreshAsync(cancellationToken);

    private async Task<IReadOnlyList<FavoriteExecutionTargetRow>> LoadAsync(
        CancellationToken cancellationToken)
    {
        var favorites = await _management
            .GetFavoriteExecutionTargetIdsAsync(
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (favorites.IsFailure)
            throw new InvalidOperationException(favorites.Error!.Message);

        var favoriteIds = favorites.Value!.ToArray();
        if (favoriteIds.Length == 0)
            return Array.Empty<FavoriteExecutionTargetRow>();

        var targets = await LoadAccessibleTargetsAsync(cancellationToken)
            .ConfigureAwait(true);

        var rows = new List<FavoriteExecutionTargetRow>(favoriteIds.Length);

        foreach (var favoriteId in favoriteIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (targets.TryGetValue(favoriteId, out var details))
            {
                var target = details.Target;
                var model = target.Model ?? target.Deployment ?? "—";
                var status = target.Resource.Lifecycle.Status == ResourceLifecycleStatus.Active
                    ? "Active"
                    : target.Resource.Lifecycle.Status.ToString();

                rows.Add(
                    new FavoriteExecutionTargetRow(
                        favoriteId,
                        target.DisplayName,
                        details.ProviderName,
                        details.AccountName,
                        model,
                        status));
            }
            else
            {
                rows.Add(
                    new FavoriteExecutionTargetRow(
                        favoriteId,
                        "Unavailable execution target",
                        "—",
                        "—",
                        "—",
                        "Unavailable"));
            }
        }

        return rows;
    }

    private async Task<IReadOnlyDictionary<ExecutionTargetId, TargetDetails>> LoadAccessibleTargetsAsync(
        CancellationToken cancellationToken)
    {
        var providers = await _management
            .ListProvidersAsync(
                _accessContext,
                includeRetired: true,
                cancellationToken: cancellationToken)
            .ConfigureAwait(true);

        if (providers.IsFailure)
            throw new InvalidOperationException(providers.Error!.Message);

        var providerMap = providers.Value!.ToDictionary(provider => provider.Id);
        var targets = new Dictionary<ExecutionTargetId, TargetDetails>();

        foreach (var provider in providers.Value!)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var accounts = await _management
                .ListProviderAccountsAsync(
                    provider.Id,
                    _accessContext,
                    includeRetired: true,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(true);

            if (accounts.IsFailure)
                throw new InvalidOperationException(accounts.Error!.Message);

            foreach (var account in accounts.Value!)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var accountTargets = await _management
                    .ListExecutionTargetsAsync(
                        account.Id,
                        _accessContext,
                        includeRetired: true,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(true);

                if (accountTargets.IsFailure)
                    throw new InvalidOperationException(accountTargets.Error!.Message);

                foreach (var target in accountTargets.Value!)
                {
                    targets[target.Id] = new TargetDetails(
                        target,
                        providerMap.TryGetValue(target.ProviderId, out var targetProvider)
                            ? targetProvider.DisplayName
                            : "Unknown provider",
                        account.DisplayName);
                }
            }
        }

        return targets;
    }

    private async Task<FavoriteExecutionTargetRow?> AddFavoriteAsync(
        FavoriteExecutionTargetRow? _,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var current = await _management
            .GetFavoriteExecutionTargetIdsAsync(
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (current.IsFailure)
            throw new InvalidOperationException(current.Error!.Message);

        using var picker = new HiveFavoriteExecutionTargetPickerForm(
            _management,
            _accessContext,
            _themeManager,
            current.Value!.ToHashSet(),
            _output);

        await picker.InitializeAsync(cancellationToken).ConfigureAwait(true);

        cancellationToken.ThrowIfCancellationRequested();

        if (picker.ShowDialog(FindForm()) != DialogResult.OK ||
            picker.SelectedExecutionTargetId is not { } targetId)
        {
            return null;
        }

        if (current.Value!.Contains(targetId))
            return null;

        var updated = current.Value!
            .Concat([targetId])
            .ToArray();

        var result = await _management
            .ReplaceFavoriteExecutionTargetIdsAsync(
                updated,
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
            throw new InvalidOperationException(result.Error!.Message);

        var target = await ResolveTargetAsync(targetId, cancellationToken)
            .ConfigureAwait(true);

        return target ?? new FavoriteExecutionTargetRow(
            targetId,
            "Unavailable execution target",
            "—",
            "—",
            "—",
            "Unavailable");
    }

    private async Task RemoveFavoriteAsync(
        FavoriteExecutionTargetRow row,
        CancellationToken cancellationToken)
    {
        var current = await _management
            .GetFavoriteExecutionTargetIdsAsync(
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (current.IsFailure)
            throw new InvalidOperationException(current.Error!.Message);

        var updated = current.Value!
            .Where(id => id != row.ExecutionTargetId)
            .ToArray();

        var result = await _management
            .ReplaceFavoriteExecutionTargetIdsAsync(
                updated,
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
            throw new InvalidOperationException(result.Error!.Message);
    }

    private async Task<FavoriteExecutionTargetRow?> ResolveTargetAsync(
        ExecutionTargetId targetId,
        CancellationToken cancellationToken)
    {
        var targets = await LoadAccessibleTargetsAsync(cancellationToken)
            .ConfigureAwait(true);

        if (!targets.TryGetValue(targetId, out var details))
            return null;

        var target = details.Target;
        var model = target.Model ?? target.Deployment ?? "—";
        var status = target.Resource.Lifecycle.Status == ResourceLifecycleStatus.Active
            ? "Active"
            : target.Resource.Lifecycle.Status.ToString();

        return new FavoriteExecutionTargetRow(
            target.Id,
            target.DisplayName,
            details.ProviderName,
            details.AccountName,
            model,
            status);
    }

    private void PageOperationFailed(
        object? sender,
        HiveCrudOperationFailedEventArgs e)
    {
        _page.SetStatus(
            "Operation failed. See technical details.",
            HiveStatusTone.Error);

        HiveUiErrorReporter.Report(
            FindForm(),
            e.Exception,
            "Favorite Execution Target operation failed",
            "The favorite execution target operation could not be completed.",
            _output,
            _themeManager);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _page.OperationFailed -= PageOperationFailed;

        base.Dispose(disposing);
    }

    internal sealed record FavoriteExecutionTargetRow(
        ExecutionTargetId ExecutionTargetId,
        string DisplayName,
        string ProviderName,
        string AccountName,
        string ModelDeployment,
        string Status)
    {
        public override string ToString() => DisplayName;
    }

    private sealed record TargetDetails(
        ExecutionTarget Target,
        string ProviderName,
        string AccountName);
}
