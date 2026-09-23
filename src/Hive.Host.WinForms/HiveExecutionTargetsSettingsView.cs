using System.Drawing;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Host.WinForms;

internal sealed class HiveExecutionTargetsSettingsView : UserControl
{
    private readonly IHiveManagementFacade _management;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveThemeManager _themeManager;
    private readonly IHiveExampleOutput? _output;
    private readonly ComboBox _providerComboBox;
    private readonly ComboBox _accountComboBox;
    private readonly HiveCrudPage<ExecutionTarget> _page;

    private IReadOnlyList<Provider> _providers = Array.Empty<Provider>();
    private IReadOnlyList<ProviderAccount> _accounts = Array.Empty<ProviderAccount>();
    private Provider? _selectedProvider;
    private ProviderAccount? _selectedAccount;
    private bool _loadingFilters;

    public HiveExecutionTargetsSettingsView(
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

        _providerComboBox = CreateComboBox();
        _accountComboBox = CreateComboBox();

        _providerComboBox.SelectedIndexChanged += ProviderComboBoxOnSelectedIndexChanged;
        _accountComboBox.SelectedIndexChanged += AccountComboBoxOnSelectedIndexChanged;

        _page = new HiveCrudPage<ExecutionTarget>
        {
            Title = "Execution Targets",
            Description =
                "Manage concrete provider endpoints, models/deployments, capabilities, and connection tests under the selected Provider Account.",
            PageSize = 25,
            AllowAdd = false,
            AllowEdit = true,
            AllowDelete = true,
            ShowRefresh = true,
            ShowSearch = true,
            SearchPlaceholder = "Search execution targets..."
        };

        _page.SetColumns(
            new HiveCrudColumn<ExecutionTarget>("Resource key", 170, item => item.Key),
            new HiveCrudColumn<ExecutionTarget>("Name", 220, item => item.DisplayName),
            new HiveCrudColumn<ExecutionTarget>("Endpoint", 300, item => item.Endpoint.ToString()),
            new HiveCrudColumn<ExecutionTarget>(
                "Model / Deployment",
                220,
                item => BuildModelDeployment(item)),
            new HiveCrudColumn<ExecutionTarget>(
                "Capabilities",
                140,
                item => item.Capabilities.Count.ToString()),
            new HiveCrudColumn<ExecutionTarget>(
                "Lifecycle",
                120,
                item => item.Resource.Lifecycle.Status.ToString()));

        _page.LoadItemsAsync = LoadAsync;
        _page.EditItemAsync = EditAsync;
        _page.DeleteItemAsync = DeleteAsync;
        _page.GetItemDisplayName = item => $"{item.DisplayName} [{item.Key}]";

        _page.OperationFailed += PageOperationFailed;

        var filterLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = new Padding(12, 8, 0, 8)
        };
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        filterLayout.Controls.Add(
            new Label
            {
                Text = "Provider",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty
            },
            0,
            0);
        filterLayout.Controls.Add(_providerComboBox, 1, 0);
        filterLayout.Controls.Add(
            new Label
            {
                Text = "Account",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(12, 0, 0, 0)
            },
            2,
            0);
        filterLayout.Controls.Add(_accountComboBox, 3, 0);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.Controls.Add(filterLayout, 0, 0);
        root.Controls.Add(_page, 0, 1);

        Controls.Add(root);
        _themeManager.Apply(this);
    }

    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await _management
            .ListProvidersAsync(
                _accessContext,
                includeRetired: true,
                cancellationToken: cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
            throw new InvalidOperationException(result.Error!.Message);

        _providers = result.Value!;

        _loadingFilters = true;
        try
        {
            PopulateProviders();
            _providerComboBox.SelectedIndex = -1;
            _providerComboBox.Text = "Select a Provider...";
            _selectedProvider = null;
        }
        finally
        {
            _loadingFilters = false;
        }

        await LoadAccountsForProviderAsync(cancellationToken).ConfigureAwait(true);
    }

    private async void ProviderComboBoxOnSelectedIndexChanged(
        object? sender,
        EventArgs e)
    {
        if (_loadingFilters)
            return;

        try
        {
            _selectedProvider =
                (_providerComboBox.SelectedItem as ProviderChoice)?.Value;

            await LoadAccountsForProviderAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            HiveUiErrorReporter.Report(
                FindForm(),
                exception,
                "Execution Targets",
                "Execution Target accounts could not be loaded.",
                _output,
                _themeManager);
        }
    }

    private async void AccountComboBoxOnSelectedIndexChanged(
        object? sender,
        EventArgs e)
    {
        if (_loadingFilters)
            return;

        try
        {
            _selectedAccount =
                (_accountComboBox.SelectedItem as AccountChoice)?.Value;

            _page.AllowAdd = _selectedAccount is not null;

            if (!IsDisposed)
                await _page.RefreshAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            HiveUiErrorReporter.Report(
                FindForm(),
                exception,
                "Execution Targets",
                "Execution Targets could not be refreshed.",
                _output,
                _themeManager);
        }
    }

    private async Task LoadAccountsForProviderAsync(
        CancellationToken cancellationToken = default)
    {
        _selectedAccount = null;
        _accounts = Array.Empty<ProviderAccount>();

        _loadingFilters = true;
        try
        {
            _accountComboBox.BeginUpdate();
            try
            {
                _accountComboBox.Items.Clear();
                _accountComboBox.SelectedIndex = -1;
                _accountComboBox.Text = "Select an Account...";

                if (_selectedProvider is null)
                {
                    _accountComboBox.Text = "Select a Provider first...";
                    return;
                }

                var result = await _management
                    .ListProviderAccountsAsync(
                        _selectedProvider.Id,
                        _accessContext,
                        includeRetired: true,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(true);

                if (result.IsFailure)
                    throw new InvalidOperationException(result.Error!.Message);

                _accounts = result.Value!;

                foreach (var account in _accounts)
                    _accountComboBox.Items.Add(new AccountChoice(account));

                _accountComboBox.SelectedIndex = -1;
                _accountComboBox.Text = "Select an Account...";
                _selectedAccount = null;
            }
            finally
            {
                _accountComboBox.EndUpdate();
            }
        }
        finally
        {
            _loadingFilters = false;
        }

        _page.AllowAdd = _selectedAccount is not null;

        if (_selectedAccount is null)
        {
            _page.SetStatus(
                _selectedProvider is null
                    ? "Select a Provider, then select a Provider Account to manage execution targets."
                    : "Select a Provider Account to manage execution targets.");
        }
        else
        {
            await _page.RefreshAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    private async Task<IReadOnlyList<ExecutionTarget>> LoadAsync(
        CancellationToken cancellationToken)
    {
        if (_selectedAccount is null)
            return Array.Empty<ExecutionTarget>();

        var result = await _management
            .ListExecutionTargetsAsync(
                _selectedAccount.Id,
                _accessContext,
                includeRetired: true,
                cancellationToken: cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
            throw new InvalidOperationException(result.Error!.Message);

        return result.Value!;
    }

    private async Task<ExecutionTarget?> EditAsync(
        ExecutionTarget? target,
        CancellationToken cancellationToken)
    {
        if (_selectedAccount is null)
        {
            HiveMessageBox.ShowInformation(
                FindForm(),
                "Select a Provider Account first.",
                "Execution Targets");

            return null;
        }

        using var editor = new HiveExecutionTargetEditorForm(
            target,
            _selectedProvider
                ?? throw new InvalidOperationException("Provider is required."),
            _selectedAccount,
            _management,
            _accessContext,
            _themeManager,
            _output);

        if (editor.ShowDialog(FindForm()) != DialogResult.OK ||
            editor.Definition is null)
        {
            return null;
        }

        var result = target is null
            ? await _management.CreateExecutionTargetAsync(
                editor.Definition,
                _accessContext,
                cancellationToken)
            : await _management.UpdateExecutionTargetAsync(
                editor.Definition,
                _accessContext,
                cancellationToken);

        if (result.IsFailure)
            throw new InvalidOperationException(result.Error!.Message);

        return result.Value;
    }

    private async Task DeleteAsync(
        ExecutionTarget target,
        CancellationToken cancellationToken)
    {
        var result = await _management
            .DeleteExecutionTargetAsync(
                target.Id,
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
            throw new InvalidOperationException(result.Error!.Message);
    }

    private void PopulateProviders()
    {
        _providerComboBox.Items.Clear();

        foreach (var provider in _providers)
            _providerComboBox.Items.Add(new ProviderChoice(provider));
    }

    private void PageOperationFailed(
        object? sender,
        HiveCrudOperationFailedEventArgs e)
    {
        _page.SetStatus(e.Exception.Message);

        HiveUiErrorReporter.Report(
            FindForm(),
            e.Exception,
            "Execution Target operation failed",
            "The execution target operation could not be completed.",
            _output,
            _themeManager);
    }

    private static ComboBox CreateComboBox() =>
        new()
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(8, 0, 0, 0)
        };

    private static string BuildModelDeployment(ExecutionTarget target)
    {
        if (target.Model is not null && target.Deployment is not null)
            return $"{target.Model} / {target.Deployment}";

        return target.Model ?? target.Deployment ?? "—";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _page.OperationFailed -= PageOperationFailed;
            _providerComboBox.SelectedIndexChanged -= ProviderComboBoxOnSelectedIndexChanged;
            _accountComboBox.SelectedIndexChanged -= AccountComboBoxOnSelectedIndexChanged;
        }

        base.Dispose(disposing);
    }

    private sealed record ProviderChoice(Provider Value)
    {
        public override string ToString() => Value.DisplayName;
    }

    private sealed record AccountChoice(ProviderAccount Value)
    {
        public override string ToString() => Value.DisplayName;
    }
}
