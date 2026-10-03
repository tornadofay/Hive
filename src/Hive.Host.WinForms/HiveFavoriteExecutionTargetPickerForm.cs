using System.Drawing;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Host.WinForms;

internal sealed class HiveFavoriteExecutionTargetPickerForm : HiveForm
{
    private readonly IHiveManagementFacade _management;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveThemeManager _themeManager;
    private readonly IHiveExampleOutput? _output;
    private readonly IReadOnlySet<ExecutionTargetId> _favoriteTargetIds;

    private readonly HiveComboBox _providerComboBox;
    private readonly HiveComboBox _accountComboBox;
    private readonly HiveComboBox _targetComboBox;
    private readonly HiveButton _addButton;
    private readonly HiveButton _cancelButton;
    private readonly Label _statusLabel;

    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly List<Provider> _providers = [];
    private bool _loading;

    public HiveFavoriteExecutionTargetPickerForm(
        IHiveManagementFacade management,
        ResourceAccessContext accessContext,
        IHiveThemeManager themeManager,
        IReadOnlySet<ExecutionTargetId> favoriteTargetIds,
        IHiveExampleOutput? output = null)
        : base(
            "Add Favorite Execution Target",
            "Choose one execution target to add to your favorites.",
            new Size(620, 420),
            new Size(560, 360),
            themeManager)
    {
        _management = management ?? throw new ArgumentNullException(nameof(management));
        _accessContext = accessContext ?? throw new ArgumentNullException(nameof(accessContext));
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _favoriteTargetIds = favoriteTargetIds ?? throw new ArgumentNullException(nameof(favoriteTargetIds));
        _output = output;

        ConfigureHeader(
            allowMove: true,
            allowClose: true,
            allowMinimize: false,
            allowMaximize: false,
            allowHelp: false,
            allowThemeToggle: true);

        SetBodyPadding(new Padding(20));

        _providerComboBox = CreateComboBox();
        _accountComboBox = CreateComboBox();
        _targetComboBox = CreateComboBox();

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty
        };

        var editor = new HiveEditorLayout
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24)
        };

        editor.AddField(
            "Provider",
            "Choose the provider first to narrow the available accounts and execution targets.",
            _providerComboBox);

        editor.AddField(
            "Account",
            "Choose the provider account to narrow the available execution targets.",
            _accountComboBox);

        editor.AddField(
            "Execution Target",
            "Choose one active execution target to add to the favorite list.",
            _targetComboBox);

        editor.AddField(
            "Status",
            "Current picker state.",
            _statusLabel);

        _cancelButton = editor.AddActionButton(
            "Cancel",
            HiveButtonStyle.Secondary,
            96);

        _addButton = editor.AddActionButton(
            "Add Favorite",
            HiveButtonStyle.Primary,
            120);

        _cancelButton.Click += CancelButtonClick;
        _addButton.Click += AddButtonClick;

        BodyPanel.Controls.Add(editor);
        ThemeManager.Apply(BodyPanel);

        _providerComboBox.SelectedIndexChanged += ProviderChanged;
        _accountComboBox.SelectedIndexChanged += AccountChanged;

        AcceptButton = _addButton;
        CancelButton = _cancelButton;

        _themeManager.Apply(this);
    }

    public ExecutionTargetId? SelectedExecutionTargetId { get; private set; }

    internal HiveComboBox ProviderSelector => _providerComboBox;
    internal HiveComboBox AccountSelector => _accountComboBox;
    internal HiveComboBox TargetSelector => _targetComboBox;
    internal HiveButton AddButton => _addButton;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetimeCts.Token);

        SetLoading(true);
        try
        {
            _providers.Clear();
            _providerComboBox.Items.Clear();
            _accountComboBox.Items.Clear();
            _targetComboBox.Items.Clear();

            var result = await _management
                .ListProvidersAsync(
                    _accessContext,
                    includeRetired: false,
                    cancellationToken: linked.Token)
                .ConfigureAwait(true);

            if (result.IsFailure)
                throw new InvalidOperationException(result.Error!.Message);

            _providers.AddRange(result.Value!);

            foreach (var provider in _providers
                         .OrderBy(item => item.DisplayName, StringComparer.Ordinal))
            {
                _providerComboBox.Items.Add(new ProviderChoice(provider));
            }

            if (_providers.Count == 0)
            {
                SetStatus("No active providers are configured.");
                return;
            }

            _providerComboBox.SelectedIndex = 0;
            await LoadAccountsAsync(linked.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
            when (linked.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            SetStatus("Favorite target picker could not be loaded.");
            HiveUiErrorReporter.Report(
                FindForm(),
                exception,
                "Add Favorite Execution Target",
                "The favorite execution-target picker could not be loaded.",
                _output,
                _themeManager);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void ProviderChanged(object? sender, EventArgs e)
    {
        if (_loading || IsDisposed || Disposing)
            return;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
        SetLoading(true);
        try
        {
            await LoadAccountsAsync(linked.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            SetStatus("Accounts could not be loaded.");
            HiveUiErrorReporter.Report(
                FindForm(),
                exception,
                "Add Favorite Execution Target",
                "Provider accounts could not be loaded.",
                _output,
                _themeManager);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void AccountChanged(object? sender, EventArgs e)
    {
        if (_loading || IsDisposed || Disposing)
            return;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
        SetLoading(true);
        try
        {
            await LoadTargetsAsync(linked.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            SetStatus("Execution targets could not be loaded.");
            HiveUiErrorReporter.Report(
                FindForm(),
                exception,
                "Add Favorite Execution Target",
                "Execution targets could not be loaded.",
                _output,
                _themeManager);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async Task LoadAccountsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _loading = true;
        try
        {
            _accountComboBox.Items.Clear();
            _targetComboBox.Items.Clear();

            var provider = (_providerComboBox.SelectedItem as ProviderChoice)?.Value;
            if (provider is null)
            {
                SetStatus("Select a Provider.");
                return;
            }

            var result = await _management
                .ListProviderAccountsAsync(
                    provider.Id,
                    _accessContext,
                    includeRetired: false,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(true);

            if (result.IsFailure)
                throw new InvalidOperationException(result.Error!.Message);

            foreach (var account in result.Value!
                         .OrderBy(item => item.DisplayName, StringComparer.Ordinal))
            {
                _accountComboBox.Items.Add(new AccountChoice(account));
            }

            if (_accountComboBox.Items.Count == 0)
            {
                SetStatus("The selected Provider has no active accounts.");
                return;
            }

            _accountComboBox.SelectedIndex = 0;
            await LoadTargetsAsync(cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task LoadTargetsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _targetComboBox.Items.Clear();

        var account = (_accountComboBox.SelectedItem as AccountChoice)?.Value;
        if (account is null)
        {
            SetStatus("Select an Account.");
            return;
        }

        var result = await _management
            .ListExecutionTargetsAsync(
                account.Id,
                _accessContext,
                includeRetired: false,
                cancellationToken: cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
            throw new InvalidOperationException(result.Error!.Message);

        foreach (var target in result.Value!
                     .Where(target => !_favoriteTargetIds.Contains(target.Id))
                     .OrderBy(item => item.DisplayName, StringComparer.Ordinal))
        {
            _targetComboBox.Items.Add(new TargetChoice(target));
        }

        if (_targetComboBox.Items.Count == 0)
        {
            SetStatus("No additional active execution targets are available for this account.");
            _addButton.Enabled = false;
            return;
        }

        _targetComboBox.SelectedIndex = 0;
        SetStatus("Select an execution target, then choose Add Favorite.");
    }

    private void AddButtonClick(object? sender, EventArgs e)
    {
        if (_loading)
            return;

        var target = (_targetComboBox.SelectedItem as TargetChoice)?.Value;
        if (target is null)
        {
            SetStatus("Select an execution target first.");
            return;
        }

        SelectedExecutionTargetId = target.Id;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void CancelButtonClick(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void SetLoading(bool value)
    {
        _loading = value;
        if (IsDisposed || Disposing)
            return;

        _providerComboBox.Enabled = !value;
        _accountComboBox.Enabled = !value && _providerComboBox.Items.Count > 0;
        _targetComboBox.Enabled = !value && _accountComboBox.Items.Count > 0;
        _addButton.Enabled = !value && _targetComboBox.SelectedItem is TargetChoice;
    }

    private void SetStatus(string message) =>
        _statusLabel.Text = message;

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
            _cancelButton.Click -= CancelButtonClick;
            _addButton.Click -= AddButtonClick;
            _lifetimeCts.Cancel();
            _lifetimeCts.Dispose();
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

    private sealed record TargetChoice(ExecutionTarget Value)
    {
        public override string ToString()
        {
            var model = Value.Model ?? Value.Deployment;
            return model is null
                ? Value.DisplayName
                : $"{Value.DisplayName} — {model}";
        }
    }
}
