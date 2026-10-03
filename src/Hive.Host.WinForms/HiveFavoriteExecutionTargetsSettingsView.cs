using System.Drawing;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Host.WinForms;

internal sealed class HiveFavoriteExecutionTargetsSettingsView : UserControl
{
    private const string AllProvidersText = "All Providers";
    private const string AllAccountsText = "All Accounts";

    private readonly IHiveManagementFacade _management;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveThemeManager _themeManager;
    private readonly IHiveExampleOutput? _output;
    private readonly HiveComboBox _providerComboBox;
    private readonly HiveComboBox _accountComboBox;
    private readonly HiveListView _targetList;
    private readonly HiveButton _saveButton;
    private readonly HiveButton _refreshButton;
    private readonly Label _statusLabel;

    private readonly List<Provider> _providers = [];
    private readonly List<ProviderAccount> _accounts = [];
    private HashSet<ExecutionTargetId> _favoriteTargetIds = [];
    private CancellationTokenSource? _loadCts;
    private bool _updatingList;
    private bool _dirty;

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
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        var header = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Favorite Execution Targets",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            Padding = new Padding(0, 8, 0, 0),
            Margin = Padding.Empty,
            AutoEllipsis = true
        };

        var filters = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

        var providerLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Provider",
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 8, 0)
        };
        var accountLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Account",
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(12, 0, 8, 0)
        };

        _providerComboBox = CreateComboBox();
        _accountComboBox = CreateComboBox();

        filters.Controls.Add(providerLabel, 0, 0);
        filters.Controls.Add(_providerComboBox, 1, 0);
        filters.Controls.Add(accountLabel, 2, 0);
        filters.Controls.Add(_accountComboBox, 3, 0);

        _targetList = new HiveListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            CheckBoxes = true,
            MultiSelect = false,
            HideSelection = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            Margin = Padding.Empty,
            AccessibleName = "Favorite Execution Targets",
            AccessibleDescription =
                "Select execution targets to include in the favorite target pool."
        };
        _targetList.Columns.Add("Execution Target", 310);
        _targetList.Columns.Add("Model / Deployment", 260);
        _targetList.Columns.Add("Management", 120);

        var footer = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Margin = Padding.Empty
        };

        _refreshButton = new HiveButton
        {
            Text = "Refresh",
            Style = HiveButtonStyle.Secondary,
            Width = 92,
            Height = 32,
            Dock = DockStyle.Right,
            Margin = new Padding(0, 0, 6, 0),
            AccessibleName = "Refresh Favorite Execution Targets",
            AccessibleDescription =
                "Reload the favorite execution-target list from Hive."
        };

        _saveButton = new HiveButton
        {
            Text = "Save Favorites",
            Style = HiveButtonStyle.Primary,
            Width = 132,
            Height = 32,
            Dock = DockStyle.Right,
            AccessibleName = "Save Favorite Execution Targets",
            AccessibleDescription =
                "Save the selected execution targets as the favorite target pool."
        };

        footer.Controls.Add(_statusLabel);
        footer.Controls.Add(_saveButton);
        footer.Controls.Add(_refreshButton);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(filters, 0, 1);
        root.Controls.Add(_targetList, 0, 2);
        root.Controls.Add(footer, 0, 3);

        _providerComboBox.SelectedIndexChanged += ProviderChanged;
        _accountComboBox.SelectedIndexChanged += AccountChanged;
        _targetList.ItemCheck += TargetItemCheck;
        _saveButton.Click += SaveButtonClick;
        _refreshButton.Click += RefreshButtonClick;

        Controls.Add(root);
        _themeManager.Apply(this);
    }

    internal HiveComboBox ProviderSelector => _providerComboBox;
    internal HiveComboBox AccountSelector => _accountComboBox;
    internal HiveListView TargetList => _targetList;
    internal HiveButton SaveButton => _saveButton;
    internal HiveButton RefreshButton => _refreshButton;
    internal Label StatusLabel => _statusLabel;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var loadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var previous = Interlocked.Exchange(ref _loadCts, loadCts);
        previous?.Cancel();

        try
        {
            _saveButton.Enabled = false;
            _refreshButton.Enabled = false;
            _providerComboBox.Items.Clear();
            _accountComboBox.Items.Clear();
            _targetList.Items.Clear();
            _providers.Clear();
            _accounts.Clear();

            var favorites = await _management
                .GetFavoriteExecutionTargetIdsAsync(
                    _accessContext,
                    loadCts.Token)
                .ConfigureAwait(true);

            if (favorites.IsFailure)
                throw new InvalidOperationException(favorites.Error!.Message);

            _favoriteTargetIds = favorites.Value!.ToHashSet();
            _dirty = false;

            var providers = await _management
                .ListProvidersAsync(
                    _accessContext,
                    includeRetired: false,
                    cancellationToken: loadCts.Token)
                .ConfigureAwait(true);

            if (providers.IsFailure)
                throw new InvalidOperationException(providers.Error!.Message);

            loadCts.Token.ThrowIfCancellationRequested();
            _providers.AddRange(providers.Value!);

            _updatingList = true;
            try
            {
                _providerComboBox.Items.Add(new ProviderChoice(null, AllProvidersText));
                foreach (var provider in _providers.OrderBy(item => item.DisplayName, StringComparer.Ordinal))
                    _providerComboBox.Items.Add(new ProviderChoice(provider, provider.DisplayName));

                _providerComboBox.SelectedIndex = _providers.Count > 0 ? 1 : 0;
            }
            finally
            {
                _updatingList = false;
            }

            await ReloadAccountsAsync(loadCts.Token).ConfigureAwait(true);
            loadCts.Token.ThrowIfCancellationRequested();
            UpdateStatus();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            if (!IsDisposed && !Disposing)
            {
                _statusLabel.Text = "Favorites could not be loaded.";
                _saveButton.Enabled = false;
                HiveUiErrorReporter.Report(
                    FindForm(),
                    exception,
                    "Favorite Execution Targets",
                    "The favorite execution-target settings could not be loaded.",
                    _output,
                    _themeManager);
            }
        }
        finally
        {
            if (ReferenceEquals(_loadCts, loadCts))
                Interlocked.CompareExchange(ref _loadCts, null, loadCts);

            loadCts.Dispose();

            if (!IsDisposed && !Disposing)
                _refreshButton.Enabled = true;
        }
    }

    private async void ProviderChanged(object? sender, EventArgs e)
    {
        if (_updatingList || IsDisposed || Disposing)
            return;

        var cts = ReplaceLoadCancellation();
        try
        {
            await ReloadAccountsAsync(cts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            HandleLoadFailure(exception);
        }
        finally
        {
            ReleaseLoadCancellation(cts);
        }
    }

    private async void AccountChanged(object? sender, EventArgs e)
    {
        if (_updatingList || IsDisposed || Disposing)
            return;

        var cts = ReplaceLoadCancellation();
        try
        {
            await ReloadTargetsAsync(cts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            HandleLoadFailure(exception);
        }
        finally
        {
            ReleaseLoadCancellation(cts);
        }
    }

    private async Task ReloadAccountsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var selectedProvider = GetSelectedProvider();

        _updatingList = true;
        try
        {
            _accounts.Clear();
            _accountComboBox.Items.Clear();

            _accountComboBox.Items.Add(new AccountChoice(null, AllAccountsText));

            if (selectedProvider is not null)
            {
                var accounts = await _management
                    .ListProviderAccountsAsync(
                        selectedProvider.Id,
                        _accessContext,
                        includeRetired: false,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(true);

                if (accounts.IsFailure)
                    throw new InvalidOperationException(accounts.Error!.Message);

                cancellationToken.ThrowIfCancellationRequested();
                _accounts.AddRange(accounts.Value!);
            }
            else
            {
                var providers = _providers.ToArray();
                foreach (var provider in providers)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var accounts = await _management
                        .ListProviderAccountsAsync(
                            provider.Id,
                            _accessContext,
                            includeRetired: false,
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(true);

                    if (accounts.IsFailure)
                        throw new InvalidOperationException(accounts.Error!.Message);

                    _accounts.AddRange(accounts.Value!);
                }
            }

            foreach (var account in _accounts
                         .OrderBy(item => item.DisplayName, StringComparer.Ordinal))
            {
                _accountComboBox.Items.Add(
                    new AccountChoice(account, account.DisplayName));
            }

            _accountComboBox.SelectedIndex = _accounts.Count > 0 ? 1 : 0;
        }
        finally
        {
            _updatingList = false;
        }

        await ReloadTargetsAsync(cancellationToken).ConfigureAwait(true);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task ReloadTargetsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var selectedProvider = GetSelectedProvider();
        var selectedAccount = GetSelectedAccount();

        _targetList.BeginUpdate();
        _updatingList = true;

        try
        {
            _targetList.Items.Clear();

            IEnumerable<ProviderAccount> accounts = _accounts.ToArray();
            if (selectedAccount is not null)
                accounts = accounts.Where(account => account.Id == selectedAccount.Id);
            else if (selectedProvider is not null)
                accounts = accounts.Where(account => account.ProviderId == selectedProvider.Id);

            var accountsToLoad = accounts.ToArray();
            var targets = new List<ExecutionTarget>();
            foreach (var account in accountsToLoad)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await _management
                    .ListExecutionTargetsAsync(
                        account.Id,
                        _accessContext,
                        includeRetired: false,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(true);

                if (result.IsFailure)
                    throw new InvalidOperationException(result.Error!.Message);

                cancellationToken.ThrowIfCancellationRequested();
                targets.AddRange(result.Value!);
            }

            foreach (var target in targets.OrderBy(item => item.DisplayName, StringComparer.Ordinal))
            {
                var model = target.Model ?? target.Deployment ?? "No model/deployment";
                var management = target.ManagementMode == ExecutionTargetManagementMode.Automatic
                    ? "Automatic"
                    : "Manual";

                var item = new ListViewItem(target.DisplayName)
                {
                    Tag = target.Id,
                    Checked = _favoriteTargetIds.Contains(target.Id),
                    ToolTipText = $"{target.DisplayName} — {model}"
                };
                item.SubItems.Add(model);
                item.SubItems.Add(management);

                _targetList.Items.Add(item);
            }
        }
        finally
        {
            _updatingList = false;
            _targetList.EndUpdate();
        }

        cancellationToken.ThrowIfCancellationRequested();
        UpdateStatus(targetCount: _targetList.Items.Count);
        _saveButton.Enabled = true;
    }

    private async void SaveButtonClick(object? sender, EventArgs e) =>
        await SaveAsync();

    private async void RefreshButtonClick(object? sender, EventArgs e) =>
        await InitializeAsync();

    private async Task SaveAsync()
    {
        if (IsDisposed || Disposing)
            return;

        _saveButton.Enabled = false;
        try
        {
            var orderedIds = _favoriteTargetIds
                .OrderBy(id => id.Value)
                .ToArray();

            var result = await _management
                .ReplaceFavoriteExecutionTargetIdsAsync(
                    orderedIds,
                    _accessContext)
                .ConfigureAwait(true);

            if (result.IsFailure)
                throw new InvalidOperationException(result.Error!.Message);

            _favoriteTargetIds = result.Value!.ToHashSet();
            _dirty = false;
            _statusLabel.Text = $"Saved {_favoriteTargetIds.Count} favorite execution target(s).";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _saveButton.Enabled = true;
            _refreshButton.Enabled = true;
            _statusLabel.Text = "Favorites could not be saved.";
            HiveUiErrorReporter.Report(
                FindForm(),
                exception,
                "Favorite Execution Targets",
                "The favorite execution-target settings could not be saved.",
                _output,
                _themeManager);
        }
        finally
        {
            if (!IsDisposed && !Disposing)
                _saveButton.Enabled = true;
        }
    }

    private void TargetItemCheck(object? sender, ItemCheckEventArgs e)
    {
        if (_updatingList ||
            e.Index < 0 ||
            e.Index >= _targetList.Items.Count)
        {
            return;
        }

        var item = _targetList.Items[e.Index];
        if (item.Tag is ExecutionTargetId targetId)
        {
            if (e.NewValue == CheckState.Checked)
                _favoriteTargetIds.Add(targetId);
            else
                _favoriteTargetIds.Remove(targetId);
        }

        _dirty = true;
        UpdateStatus(_targetList.Items.Count);
    }

    private void UpdateStatus(int? targetCount = null)
    {
        var count = targetCount ?? _targetList.Items.Count;
        var dirty = _dirty ? "Unsaved changes. " : string.Empty;

        _statusLabel.Text =
            $"{dirty}{_favoriteTargetIds.Count} favorite(s) configured; {count} target(s) shown.";
    }

    private Provider? GetSelectedProvider() =>
        (_providerComboBox.SelectedItem as ProviderChoice)?.Value;

    private ProviderAccount? GetSelectedAccount() =>
        (_accountComboBox.SelectedItem as AccountChoice)?.Value;

    private CancellationTokenSource ReplaceLoadCancellation()
    {
        var cts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _loadCts, cts);
        previous?.Cancel();
        return cts;
    }

    private void ReleaseLoadCancellation(CancellationTokenSource cts)
    {
        if (ReferenceEquals(_loadCts, cts))
            Interlocked.CompareExchange(ref _loadCts, null, cts);

        cts.Dispose();
    }

    private void HandleLoadFailure(Exception exception)
    {
        if (IsDisposed || Disposing)
            return;

        _statusLabel.Text = "Favorite targets could not be loaded.";
        HiveUiErrorReporter.Report(
            FindForm(),
            exception,
            "Favorite Execution Targets",
            "The favorite execution-target list could not be refreshed.",
            _output,
            _themeManager);
    }

    private static HiveComboBox CreateComboBox() =>
        new()
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _providerComboBox.SelectedIndexChanged -= ProviderChanged;
            _accountComboBox.SelectedIndexChanged -= AccountChanged;
            _targetList.ItemCheck -= TargetItemCheck;
            _saveButton.Click -= SaveButtonClick;
            _refreshButton.Click -= RefreshButtonClick;

            var cts = Interlocked.Exchange(ref _loadCts, null);
            cts?.Cancel();
        }

        base.Dispose(disposing);
    }

    private sealed record ProviderChoice(Provider? Value, string Display)
    {
        public override string ToString() => Display;
    }

    private sealed record AccountChoice(ProviderAccount? Value, string Display)
    {
        public override string ToString() => Display;
    }
}
