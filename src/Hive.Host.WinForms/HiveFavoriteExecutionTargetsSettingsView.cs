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

    private readonly HiveListView _favoriteList;
    private readonly HiveButton _addButton;
    private readonly HiveButton _removeButton;
    private readonly HiveButton _refreshButton;
    private readonly Label _statusLabel;

    private readonly CancellationTokenSource _lifetimeCts = new();
    private bool _loading;

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

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        var header = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Favorite Execution Targets",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            Padding = new Padding(0, 8, 0, 0),
            Margin = Padding.Empty,
            AutoEllipsis = true
        };

        _favoriteList = new HiveListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            MultiSelect = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            Margin = Padding.Empty,
            AccessibleName = "Favorite Execution Targets",
            AccessibleDescription = "The execution targets currently saved as favorites."
        };
        _favoriteList.Columns.Add("Execution Target", 270);
        _favoriteList.Columns.Add("Provider", 190);
        _favoriteList.Columns.Add("Account", 190);
        _favoriteList.Columns.Add("Model / Deployment", 230);
        _favoriteList.Columns.Add("Status", 110);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = new Padding(0, 8, 0, 8)
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Margin = Padding.Empty
        };

        _addButton = new HiveButton
        {
            Text = "Add Favorite",
            Style = HiveButtonStyle.Primary,
            Dock = DockStyle.Fill,
            Margin = new Padding(4, 0, 4, 0),
            AccessibleName = "Add Favorite Execution Target",
            AccessibleDescription = "Choose an execution target through Provider and Account filters and add it to favorites."
        };

        _removeButton = new HiveButton
        {
            Text = "Remove",
            Style = HiveButtonStyle.Secondary,
            Dock = DockStyle.Fill,
            Margin = new Padding(4, 0, 4, 0),
            AccessibleName = "Remove Favorite Execution Target",
            AccessibleDescription = "Remove the selected execution target from favorites."
        };

        _refreshButton = new HiveButton
        {
            Text = "Refresh",
            Style = HiveButtonStyle.Secondary,
            Dock = DockStyle.Fill,
            Margin = new Padding(4, 0, 0, 0),
            AccessibleName = "Refresh Favorite Execution Targets",
            AccessibleDescription = "Reload the saved favorite execution targets."
        };

        footer.Controls.Add(_statusLabel, 0, 0);
        footer.Controls.Add(_addButton, 1, 0);
        footer.Controls.Add(_removeButton, 2, 0);
        footer.Controls.Add(_refreshButton, 3, 0);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(_favoriteList, 0, 1);
        root.Controls.Add(footer, 0, 2);

        Controls.Add(root);

        _favoriteList.SelectedIndexChanged += FavoriteSelectionChanged;
        _addButton.Click += AddButtonClick;
        _removeButton.Click += RemoveButtonClick;
        _refreshButton.Click += RefreshButtonClick;

        _removeButton.Enabled = false;
        _themeManager.Apply(this);
    }

    internal HiveListView FavoriteList => _favoriteList;
    internal HiveButton AddButton => _addButton;
    internal HiveButton RemoveButton => _removeButton;
    internal HiveButton RefreshButton => _refreshButton;
    internal Label StatusLabel => _statusLabel;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetimeCts.Token);

        SetLoading(true);
        try
        {
            var favorites = await _management
                .GetFavoriteExecutionTargetIdsAsync(
                    _accessContext,
                    linked.Token)
                .ConfigureAwait(true);

            if (favorites.IsFailure)
                throw new InvalidOperationException(favorites.Error!.Message);

            var favoriteIds = favorites.Value!.ToArray();
            if (favoriteIds.Length == 0)
            {
                PopulateFavorites(
                    favoriteIds,
                    new Dictionary<ExecutionTargetId, TargetDetails>());
                return;
            }

            var targets = await LoadAccessibleTargetsAsync(linked.Token)
                .ConfigureAwait(true);

            PopulateFavorites(favoriteIds, targets);
        }
        catch (OperationCanceledException)
            when (linked.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _favoriteList.Items.Clear();
            SetStatus("Favorite execution targets could not be loaded.");
            HiveUiErrorReporter.Report(
                FindForm(),
                exception,
                "Favorite Execution Targets",
                "The saved favorite execution targets could not be loaded.",
                _output,
                _themeManager);
        }
        finally
        {
            SetLoading(false);
        }
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

    private void PopulateFavorites(
        IReadOnlyList<ExecutionTargetId> favoriteIds,
        IReadOnlyDictionary<ExecutionTargetId, TargetDetails> targets)
    {
        _favoriteList.BeginUpdate();
        try
        {
            _favoriteList.Items.Clear();

            foreach (var favoriteId in favoriteIds)
            {
                if (targets.TryGetValue(favoriteId, out var details))
                {
                    var target = details.Target;
                    var model = target.Model ?? target.Deployment ?? "—";
                    var status = target.Resource.Lifecycle.Status == ResourceLifecycleStatus.Active
                        ? "Active"
                        : target.Resource.Lifecycle.Status.ToString();

                    var item = new ListViewItem(target.DisplayName)
                    {
                        Tag = favoriteId,
                        ToolTipText = $"{details.ProviderName} / {details.AccountName} — {model}"
                    };
                    item.SubItems.Add(details.ProviderName);
                    item.SubItems.Add(details.AccountName);
                    item.SubItems.Add(model);
                    item.SubItems.Add(status);
                    _favoriteList.Items.Add(item);
                }
                else
                {
                    var item = new ListViewItem("Unavailable execution target")
                    {
                        Tag = favoriteId,
                        ToolTipText = favoriteId.Value.ToString()
                    };
                    item.SubItems.Add("—");
                    item.SubItems.Add("—");
                    item.SubItems.Add("—");
                    item.SubItems.Add("Unavailable");
                    _favoriteList.Items.Add(item);
                }
            }
        }
        finally
        {
            _favoriteList.EndUpdate();
        }

        _removeButton.Enabled = _favoriteList.SelectedItems.Count == 1;
        SetStatus(
            favoriteIds.Count == 0
                ? "No favorite execution targets configured."
                : $"{favoriteIds.Count} favorite execution target(s) configured.");
    }

    private async void AddButtonClick(object? sender, EventArgs e)
    {
        if (_loading || IsDisposed || Disposing)
            return;

        var favoriteIds = GetFavoriteIdsFromList();

        using var picker = new HiveFavoriteExecutionTargetPickerForm(
            _management,
            _accessContext,
            _themeManager,
            favoriteIds,
            _output);

        await picker.InitializeAsync(_lifetimeCts.Token).ConfigureAwait(true);
        if (IsDisposed || Disposing ||
            picker.DialogResult == DialogResult.Cancel)
        {
            return;
        }

        if (picker.ShowDialog(FindForm()) != DialogResult.OK ||
            picker.SelectedExecutionTargetId is not { } targetId)
        {
            return;
        }

        if (favoriteIds.Contains(targetId))
            return;

        var updated = favoriteIds
            .Concat([targetId])
            .ToArray();

        await PersistFavoritesAsync(updated).ConfigureAwait(true);
    }

    private async void RemoveButtonClick(object? sender, EventArgs e)
    {
        if (_loading || IsDisposed || Disposing ||
            _favoriteList.SelectedItems.Count != 1)
        {
            return;
        }

        var item = _favoriteList.SelectedItems[0];
        if (item.Tag is not ExecutionTargetId targetId)
            return;

        var targetName = item.Text;
        var result = HiveMessageBox.ShowQuestion(
            FindForm(),
            $"Remove '{targetName}' from Favorite Execution Targets?",
            "Remove Favorite",
            MessageBoxButtons.YesNo,
            _themeManager);

        if (result != DialogResult.Yes)
            return;

        var updated = GetFavoriteIdsFromList()
            .Where(id => id != targetId)
            .ToArray();

        await PersistFavoritesAsync(updated).ConfigureAwait(true);
    }

    private async void RefreshButtonClick(object? sender, EventArgs e)
    {
        if (_loading || IsDisposed || Disposing)
            return;

        await InitializeAsync(_lifetimeCts.Token).ConfigureAwait(true);
    }

    private async Task PersistFavoritesAsync(IReadOnlyList<ExecutionTargetId> favoriteIds)
    {
        SetLoading(true);
        try
        {
            var result = await _management
                .ReplaceFavoriteExecutionTargetIdsAsync(
                    favoriteIds,
                    _accessContext,
                    _lifetimeCts.Token)
                .ConfigureAwait(true);

            if (result.IsFailure)
                throw new InvalidOperationException(result.Error!.Message);

            await InitializeAsync(_lifetimeCts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            HiveUiErrorReporter.Report(
                FindForm(),
                exception,
                "Favorite Execution Targets",
                "The favorite execution-target change could not be saved.",
                _output,
                _themeManager);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private IReadOnlyList<ExecutionTargetId> GetFavoriteIdsFromList() =>
        _favoriteList.Items
            .Cast<ListViewItem>()
            .Select(item => item.Tag)
            .OfType<ExecutionTargetId>()
            .ToArray();

    private void FavoriteSelectionChanged(object? sender, EventArgs e)
    {
        if (!_loading && !IsDisposed && !Disposing)
            _removeButton.Enabled = _favoriteList.SelectedItems.Count == 1;
    }

    private void SetLoading(bool value)
    {
        _loading = value;
        if (IsDisposed || Disposing)
            return;

        _addButton.Enabled = !value;
        _removeButton.Enabled = !value && _favoriteList.SelectedItems.Count == 1;
        _refreshButton.Enabled = !value;
    }

    private void SetStatus(string message) =>
        _statusLabel.Text = message;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _favoriteList.SelectedIndexChanged -= FavoriteSelectionChanged;
            _addButton.Click -= AddButtonClick;
            _removeButton.Click -= RemoveButtonClick;
            _refreshButton.Click -= RefreshButtonClick;
            _lifetimeCts.Cancel();
            _lifetimeCts.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed record TargetDetails(
        ExecutionTarget Target,
        string ProviderName,
        string AccountName);
}
