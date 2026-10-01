using System.Drawing;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Host.WinForms;

internal sealed class HiveExecutionTargetEditorForm : HiveForm
{
    private readonly ExecutionTarget? _existing;
    private readonly Provider _provider;
    private readonly ProviderAccount _account;
    private readonly IHiveManagementFacade _management;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveExampleOutput? _output;
    private readonly Control _providerAccountSummary;
    private readonly TextBox _keyTextBox;
    private readonly TextBox _nameTextBox;
    private readonly TextBox _endpointTextBox;
    private readonly TextBox _deploymentTextBox;
    private readonly HiveCapabilityEditor _capabilityEditor;
    private readonly ComboBox _managementModeComboBox;
    private readonly Label _testStatus;
    private readonly HiveButton _testButton;
    private readonly HiveProviderModelDiscoveryPanel _discoveryPanel;
    private HiveStatusTone _testStatusTone = HiveStatusTone.Neutral;
    private CancellationTokenSource? _testCts;

    public HiveExecutionTargetEditorForm(
        ExecutionTarget? target,
        Provider provider,
        ProviderAccount account,
        IHiveManagementFacade management,
        ResourceAccessContext accessContext,
        IHiveThemeManager themeManager,
        IHiveExampleOutput? output = null)
        : base(
            target is null ? "New Execution Target" : "Edit Execution Target",
            "Concrete endpoint, model/deployment, capabilities, and connection target",
            new Size(820, 720),
            new Size(700, 600),
            themeManager)
    {
        _existing = target;
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _account = account ?? throw new ArgumentNullException(nameof(account));
        _management = management ?? throw new ArgumentNullException(nameof(management));
        _accessContext = accessContext ?? throw new ArgumentNullException(nameof(accessContext));
        _output = output;

        ConfigureHeader(
            allowMove: true,
            allowClose: true,
            allowMinimize: false,
            allowMaximize: false,
            allowHelp: false,
            allowThemeToggle: true);

        SetBodyPadding(new Padding(16));

        var editor = new HiveEditorLayout();

        _providerAccountSummary = CreateProviderAccountSummary(
            _provider.DisplayName,
            _account.DisplayName,
            themeManager);
        _keyTextBox = CreateTextBox();
        _keyTextBox.PlaceholderText = "e.g. llama-production";
        _nameTextBox = CreateTextBox();
        _nameTextBox.PlaceholderText = "e.g. Production Llama";
        _endpointTextBox = CreateTextBox();
        _endpointTextBox.PlaceholderText = "https://api.example.com/v1";
        _deploymentTextBox = CreateTextBox();
        _deploymentTextBox.PlaceholderText = "Optional deployment name";
        _capabilityEditor = new HiveCapabilityEditor(themeManager);
        _capabilityEditor.Configure(
            target?.Capabilities ?? Array.Empty<CapabilityStateEntry>(),
            discovery: null,
            automatic: target?.ManagementMode == ExecutionTargetManagementMode.Automatic);

        _managementModeComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Height = 32,
            IntegralHeight = false
        };

        foreach (var mode in Enum.GetValues<ExecutionTargetManagementMode>())
            _managementModeComboBox.Items.Add(mode);

        _managementModeComboBox.SelectedItem =
            target?.ManagementMode ?? ExecutionTargetManagementMode.Manual;
        var initialEndpoint =
            target?.Endpoint ??
            BuiltInProviderCatalog.Find(_provider.Key)?.DefaultEndpoint;

        _discoveryPanel = new HiveProviderModelDiscoveryPanel(
            _management,
            _provider.Id,
            _account.Id,
            initialEndpoint,
            target?.Model,
            _accessContext,
            themeManager,
            _output);

        _testStatus = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Margin = Padding.Empty,
            Padding = new Padding(0, 0, 8, 0)
        };

        _keyTextBox.Text = target?.Key ?? string.Empty;
        _nameTextBox.Text = target?.DisplayName ?? string.Empty;
        _endpointTextBox.Text =
            target?.Endpoint.ToString() ??
            BuiltInProviderCatalog.Find(_provider.Key)?.DefaultEndpoint?.ToString() ??
            string.Empty;
        _deploymentTextBox.Text = target?.Deployment ?? string.Empty;

        if (target is not null)
            SetReadOnlyVisualState(_keyTextBox, themeManager);

        _endpointTextBox.TextChanged += EndpointTextBoxOnTextChanged;
        _discoveryPanel.ModelSelected += DiscoveryPanelOnModelSelected;
        _discoveryPanel.ModelSelector.TextChanged += ModelSelectorTextChanged;

        _managementModeComboBox.SelectedIndexChanged += ManagementModeOnChanged;

        editor.AddField(
            "Provider / Account",
            "The Provider and Provider Account selected by the Execution Targets page. Both relationships are read-only here.",
            _providerAccountSummary,
            52);

        editor.AddField(
            "Resource key",
            "Stable internal Execution Target identifier. This is NOT an API key. It becomes read-only after creation.",
            _keyTextBox);

        editor.AddField(
            "Display name",
            "Human-readable execution target name.",
            _nameTextBox);

        editor.AddField(
            "Endpoint",
            "The actual provider API endpoint. Enter an absolute HTTP/HTTPS URI; credentials must never be embedded in it.",
            _endpointTextBox);

        editor.AddField(
            "Model",
            "Select a discovered model or type a custom model identifier. Either Model or Deployment must be supplied.",
            _discoveryPanel,
            168);

        editor.AddField(
            "Deployment",
            "Optional deployment identifier for providers that use deployments.",
            _deploymentTextBox);

        editor.AddField(
            "Capabilities",
            "Known Hive capabilities are configured with structured Supported / Unsupported / Unknown states. " +
            "Discovered, configured override, and effective states are shown separately. " +
            "Automatic targets are discovery-managed.",
            _capabilityEditor,
            260);

        editor.AddField(
            "Management",
            "Automatic targets are maintained from successful provider discovery. Manual targets are never overwritten. " +
            "Choose Manual when administrator configuration should own the target.",
            _managementModeComboBox);

        var save = editor.AddActionButton(
            "Save",
            HiveButtonStyle.Primary,
            96);
        var cancel = editor.AddActionButton(
            "Cancel",
            HiveButtonStyle.Secondary,
            96);
        _testButton = editor.AddActionButton(
            "Test",
            HiveButtonStyle.Secondary,
            86);
        _testButton.Enabled = target is not null;
        _testButton.Click += async (_, _) => await TestConnectionAsync();

        var testStatusHost = new Panel
        {
            Width = 300,
            Height = 36,
            Margin = new Padding(8, 0, 0, 0),
            Padding = Padding.Empty
        };
        testStatusHost.Controls.Add(_testStatus);
        editor.FooterPanel.Controls.Add(testStatusHost);

        SetTestStatus(
            target is null
                ? "Save the target before testing the connection."
                : "Connection test not run.",
            HiveStatusTone.Neutral);

        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        save.Click += (_, _) => Save();

        AcceptButton = save;
        CancelButton = cancel;

        BodyPanel.Controls.Add(editor);
        ThemeManager.Apply(BodyPanel);

        Load += async (_, _) =>
        {
            await _discoveryPanel.InitializeAsync();

            _capabilityEditor.Configure(
                _existing?.Capabilities ?? Array.Empty<CapabilityStateEntry>(),
                _discoveryPanel.SelectedModel,
                GetSelectedManagementMode() == ExecutionTargetManagementMode.Automatic);
        };
    }

    public ExecutionTarget? Definition { get; private set; }

    internal HiveProviderModelDiscoveryPanel DiscoveryPanel => _discoveryPanel;

    internal ComboBox ModelSelector => _discoveryPanel.ModelSelector;

    internal TextBox EndpointTextBox => _endpointTextBox;

    internal HiveButton TestButton => _testButton;

    internal Label TestStatusLabel => _testStatus;

    internal ComboBox ManagementModeSelector => _managementModeComboBox;

    internal HiveCapabilityEditor CapabilityEditor => _capabilityEditor;

    protected override void OnThemeChanged(HiveThemeDefinition theme) =>
        ApplyTestStatusVisual(theme);

    private void DiscoveryPanelOnModelSelected(
        object? sender,
        ProviderModelSelectedEventArgs e)
    {
        if (GetSelectedManagementMode() == ExecutionTargetManagementMode.Automatic)
        {
            _capabilityEditor.Configure(
                e.Model.DiscoveredCapabilities,
                e.Model,
                automatic: true);
        }
        else
        {
            _capabilityEditor.SetDiscovery(e.Model);
        }
    }

    private void ManagementModeOnChanged(
        object? sender,
        EventArgs e)
    {
        if (_existing is null)
            return;

        var automatic = GetSelectedManagementMode() ==
                        ExecutionTargetManagementMode.Automatic;

        _capabilityEditor.Configure(
            _existing.Capabilities,
            _discoveryPanel.SelectedModel,
            automatic);
    }

    private void EndpointTextBoxOnTextChanged(
        object? sender,
        EventArgs e)
    {
        _discoveryPanel.MarkEndpointConfigurationChanged();
        _endpointTextBox.TextChanged -= EndpointTextBoxOnTextChanged;
    }

    private void ModelSelectorTextChanged(
        object? sender,
        EventArgs e)
    {
        var selectedModel = _discoveryPanel.SelectedModel;
        var isSelectedModelText =
            selectedModel is not null &&
            string.Equals(
                selectedModel.ModelId,
                _discoveryPanel.ModelSelector.Text,
                StringComparison.Ordinal);

        if (isSelectedModelText)
            return;

        if (GetSelectedManagementMode() == ExecutionTargetManagementMode.Automatic)
        {
            _capabilityEditor.Configure(
                Array.Empty<CapabilityStateEntry>(),
                discovery: null,
                automatic: true);
        }
        else
        {
            _capabilityEditor.SetDiscovery(null);
        }
    }

    private void SetTestStatus(string text, HiveStatusTone tone)
    {
        _testStatusTone = tone;
        _testStatus.Text = text;
        ApplyTestStatusVisual(ThemeManager.Theme);
    }

    private void ApplyTestStatusVisual(HiveThemeDefinition theme)
    {
        _testStatus.ForeColor = _testStatusTone switch
        {
            HiveStatusTone.Information => theme.VisualStates.Information,
            HiveStatusTone.Success => theme.VisualStates.Success,
            HiveStatusTone.Warning => theme.VisualStates.Warning,
            HiveStatusTone.Error => theme.VisualStates.Error,
            _ => theme.Palette.MutedText
        };
    }

    private async Task TestConnectionAsync()
    {
        if (_existing is null || IsDisposed || Disposing)
            return;

        var testCts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(
            ref _testCts,
            testCts);
        previous?.Cancel();

        _testButton.Enabled = false;
        SetTestStatus("Testing connection...", HiveStatusTone.Information);

        try
        {
            var result = await _management
                .TestExecutionTargetConnectionAsync(
                    _existing.Id,
                    _accessContext,
                    testCts.Token)
                .ConfigureAwait(true);

            if (testCts.IsCancellationRequested ||
                IsDisposed ||
                Disposing)
            {
                return;
            }

            if (result.IsFailure)
            {
                var message = $"Connection test failed: {result.Error!.Message}";
                SetTestStatus(message, HiveStatusTone.Error);
                HiveUiErrorReporter.Report(
                    this,
                    message,
                    "Execution Target",
                    _output,
                    ThemeManager);
                return;
            }

            SetTestStatus("Connection test succeeded.", HiveStatusTone.Success);
        }
        catch (OperationCanceledException)
            when (testCts.IsCancellationRequested ||
                  IsDisposed ||
                  Disposing)
        {
            return;
        }
        catch (Exception exception)
        {
            if (!IsDisposed && !Disposing)
            {
                SetTestStatus(
                    "Connection test failed. See technical details.",
                    HiveStatusTone.Error);
                HiveUiErrorReporter.Report(
                    this,
                    exception,
                    "Execution Target",
                    "The execution-target connection test failed.",
                    _output,
                    ThemeManager);
            }
        }
        finally
        {
            if (ReferenceEquals(_testCts, testCts))
                Interlocked.CompareExchange(
                    ref _testCts,
                    null,
                    testCts);

            testCts.Dispose();

            if (!IsDisposed && !Disposing)
                _testButton.Enabled = true;
        }
    }

    private void Save()
    {
        try
        {
            var key = _keyTextBox.Text.Trim();
            var name = _nameTextBox.Text.Trim();

            if (!Uri.TryCreate(_endpointTextBox.Text.Trim(), UriKind.Absolute, out var endpoint))
                throw new InvalidOperationException(
                    "Endpoint must be an absolute HTTP or HTTPS URI.");

            var capabilities = _capabilityEditor.GetConfiguredCapabilities();

            var managementMode = GetSelectedManagementMode();

            Definition = _existing is null
                ? new ExecutionTarget(
                    HiveSettingsResourceFactory.CreateEnvelope(
                        ResourceKind.ExecutionTarget,
                        ExecutionTargetId.New(),
                        _accessContext),
                    _provider.Id,
                    _account.Id,
                    key,
                    name,
                    endpoint,
                    NormalizeOptional(_discoveryPanel.ModelSelector.Text),
                    NormalizeOptional(_deploymentTextBox.Text),
                    capabilities)
                    .WithManagementMode(managementMode)
                : _existing
                    .WithDisplayName(name)
                    .WithEndpoint(endpoint)
                    .WithModel(NormalizeOptional(_discoveryPanel.ModelSelector.Text))
                    .WithDeployment(NormalizeOptional(_deploymentTextBox.Text))
                    .WithCapabilities(capabilities)
                    .WithManagementMode(managementMode);

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            HiveUiErrorReporter.Report(
                this,
                exception,
                "Execution Target",
                "The Execution Target could not be saved.",
                _output,
                ThemeManager);
        }
    }

    private ExecutionTargetManagementMode GetSelectedManagementMode()
    {
        if (_managementModeComboBox.SelectedItem is not ExecutionTargetManagementMode mode)
        {
            throw new InvalidOperationException(
                "Execution target management mode is required.");
        }

        return mode;
    }

    private static string? NormalizeOptional(string text) =>
        string.IsNullOrWhiteSpace(text)
            ? null
            : text.Trim();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _testCts?.Cancel();
            _testCts = null;

            _managementModeComboBox.SelectedIndexChanged -= ManagementModeOnChanged;

            _discoveryPanel.ModelSelected -= DiscoveryPanelOnModelSelected;
            _discoveryPanel.ModelSelector.TextChanged -= ModelSelectorTextChanged;
            _endpointTextBox.TextChanged -= EndpointTextBoxOnTextChanged;
        }

        base.Dispose(disposing);
    }

    private static TextBox CreateTextBox() =>
        new()
        {
            Dock = DockStyle.Fill,
            Height = 32,
            BorderStyle = BorderStyle.FixedSingle
        };

    private static Control CreateProviderAccountSummary(
        string provider,
        string account,
        IHiveThemeManager themeManager)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = new Padding(0, 8, 0, 8)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        panel.Controls.Add(CreateContextLabel("Provider", themeManager), 0, 0);
        panel.Controls.Add(CreateContextValueLabel(provider, themeManager), 1, 0);
        panel.Controls.Add(CreateContextLabel("Account", themeManager), 2, 0);
        panel.Controls.Add(CreateContextValueLabel(account, themeManager), 3, 0);

        return panel;
    }

    private static Label CreateContextLabel(
        string text,
        IHiveThemeManager themeManager) =>
        new()
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(
                SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont,
                FontStyle.Bold),
            ForeColor = themeManager.Theme.Palette.Text,
            Margin = Padding.Empty
        };

    private static Label CreateContextValueLabel(
        string text,
        IHiveThemeManager themeManager) =>
        new()
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            ForeColor = themeManager.Theme.Palette.MutedText,
            Margin = Padding.Empty,
            Padding = new Padding(4, 0, 12, 0)
        };

    private static void SetReadOnlyVisualState(
        TextBox textBox,
        IHiveThemeManager themeManager)
    {
        textBox.ReadOnly = true;
        textBox.TabStop = false;
        textBox.Cursor = Cursors.Arrow;
        textBox.BackColor = themeManager.Theme.Palette.ElevatedSurface;
        textBox.ForeColor = themeManager.Theme.Palette.MutedText;
    }
}
