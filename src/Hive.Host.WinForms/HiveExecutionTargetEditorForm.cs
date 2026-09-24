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
    private readonly TextBox _providerTextBox;
    private readonly TextBox _accountTextBox;
    private readonly TextBox _keyTextBox;
    private readonly TextBox _nameTextBox;
    private readonly TextBox _endpointTextBox;
    private readonly TextBox _modelTextBox;
    private readonly TextBox _deploymentTextBox;
    private readonly TextBox _capabilitiesTextBox;
    private readonly Label _testStatus;
    private readonly HiveButton _testButton;
    private HiveStatusTone _testStatusTone = HiveStatusTone.Neutral;

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

        SetBodyPadding(new Padding(20));

        var editor = new HiveEditorLayout();

        _providerTextBox = CreateReadOnlyTextBox(_provider.DisplayName, themeManager);
        _accountTextBox = CreateReadOnlyTextBox(_account.DisplayName, themeManager);
        _keyTextBox = CreateTextBox();
        _keyTextBox.PlaceholderText = "e.g. llama-production";
        _nameTextBox = CreateTextBox();
        _nameTextBox.PlaceholderText = "e.g. Production Llama";
        _endpointTextBox = CreateTextBox();
        _endpointTextBox.PlaceholderText = "https://api.example.com/v1";
        _modelTextBox = CreateTextBox();
        _modelTextBox.PlaceholderText = "e.g. meta/llama-3.3-70b-instruct";
        _deploymentTextBox = CreateTextBox();
        _deploymentTextBox.PlaceholderText = "Optional deployment name";
        _capabilitiesTextBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            Height = 100
        };
        _testStatus = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Bottom,
            Height = 22
        };

        _keyTextBox.Text = target?.Key ?? string.Empty;
        _nameTextBox.Text = target?.DisplayName ?? string.Empty;
        _endpointTextBox.Text = target?.Endpoint.ToString() ?? string.Empty;
        _modelTextBox.Text = target?.Model ?? string.Empty;
        _deploymentTextBox.Text = target?.Deployment ?? string.Empty;
        _capabilitiesTextBox.Text = FormatCapabilities(
            target?.Capabilities ?? Array.Empty<CapabilityStateEntry>());

        if (target is not null)
        {
            SetReadOnlyVisualState(_keyTextBox, themeManager);
        }

        editor.AddField(
            "Provider",
            "Fixed parent Provider selected by the Settings page. This relationship is read-only.",
            _providerTextBox);

        editor.AddField(
            "Provider Account",
            "Fixed credential/account boundary selected by the Settings page. This relationship is read-only.",
            _accountTextBox);

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
            "Model identifier. Either Model or Deployment must be supplied.",
            _modelTextBox);

        editor.AddField(
            "Deployment",
            "Optional deployment identifier for providers that use deployments.",
            _deploymentTextBox);

        editor.AddField(
            "Capabilities",
            "One entry per line using capability=Supported, capability=Unsupported, or capability=Unknown.",
            _capabilitiesTextBox,
            118);

        var save = editor.AddActionButton(
            target is null ? "Create" : "Save",
            HiveButtonStyle.Primary,
            96);
        var cancel = editor.AddActionButton(
            "Cancel",
            HiveButtonStyle.Secondary,
            96);
        _testButton = editor.AddActionButton(
            "Test connection",
            HiveButtonStyle.Secondary,
            124);
        _testButton.Visible = target is not null;
        _testButton.Click += async (_, _) => await TestConnectionAsync();

        SetTestStatus(
            target is null
                ? "Save the target before testing its connection."
                : "Connection test not run.",
            HiveStatusTone.Neutral);

        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        save.Click += (_, _) => Save();

        editor.AddField(
            "Test status",
            "Tests run through Hive.Management using this target and its referenced credential.",
            _testStatus,
            62);

        AcceptButton = save;
        CancelButton = cancel;

        BodyPanel.Controls.Add(editor);
        ThemeManager.Apply(BodyPanel);
    }

    public ExecutionTarget? Definition { get; private set; }

    protected override void OnThemeChanged(HiveThemeDefinition theme) =>
        ApplyTestStatusVisual(theme);

    private void SetTestStatus(string text, HiveStatusTone tone)
    {
        _testStatusTone = tone;
        _testStatus.Text = text;
        ApplyTestStatusVisual(_themeManager.Theme);
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
        if (_existing is null)
            return;

        _testButton.Enabled = false;
        SetTestStatus("Testing connection...", HiveStatusTone.Information);
        try
        {
            var result = await _management
                .TestExecutionTargetConnectionAsync(
                    _existing.Id,
                    _accessContext)
                .ConfigureAwait(true);

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
        catch (Exception exception)
        {
            SetTestStatus($"Connection test failed: {exception.Message}", HiveStatusTone.Error);
            HiveUiErrorReporter.Report(
                this,
                exception,
                "Execution Target",
                "The execution-target connection test failed.",
                _output,
                ThemeManager);
        }
        finally
        {
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

            var capabilities = ParseCapabilities(_capabilitiesTextBox.Text);

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
                    NormalizeOptional(_modelTextBox.Text),
                    NormalizeOptional(_deploymentTextBox.Text),
                    capabilities)
                : _existing
                    .WithDisplayName(name)
                    .WithEndpoint(endpoint)
                    .WithModel(NormalizeOptional(_modelTextBox.Text))
                    .WithDeployment(NormalizeOptional(_deploymentTextBox.Text))
                    .WithCapabilities(capabilities);

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

    private static IReadOnlyList<CapabilityStateEntry> ParseCapabilities(
        string text)
    {
        var entries = new List<CapabilityStateEntry>();
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rawLine in text.Split(
                     new[] { '\r', '\n' },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            var separator = line.IndexOf('=');

            if (separator <= 0 || separator == line.Length - 1)
                throw new InvalidOperationException(
                    $"Invalid capability entry '{line}'. Use capability=State.");

            var keyText = line[..separator].Trim();
            var stateText = line[(separator + 1)..].Trim();

            if (!Enum.TryParse<CapabilityState>(
                    stateText,
                    ignoreCase: true,
                    out var state))
            {
                throw new InvalidOperationException(
                    $"Invalid capability state '{stateText}'. Use Supported, Unsupported, or Unknown.");
            }

            var capability = new CapabilityKey(keyText);

            if (!keys.Add(capability.Value))
                throw new InvalidOperationException(
                    $"Capability '{capability.Value}' is listed more than once.");

            entries.Add(new CapabilityStateEntry(capability, state));
        }

        return entries;
    }

    private static string FormatCapabilities(
        IReadOnlyList<CapabilityStateEntry> capabilities) =>
        string.Join(
            Environment.NewLine,
            capabilities.Select(
                item => $"{item.Capability.Value}={item.State}"));

    private static string? NormalizeOptional(string text) =>
        string.IsNullOrWhiteSpace(text)
            ? null
            : text.Trim();

    private static TextBox CreateTextBox() =>
        new()
        {
            Dock = DockStyle.Fill,
            Height = 32,
            BorderStyle = BorderStyle.FixedSingle
        };

    private static TextBox CreateReadOnlyTextBox(
        string value,
        IHiveThemeManager themeManager)
    {
        var box = CreateTextBox();
        box.Text = value;
        SetReadOnlyVisualState(box, themeManager);
        return box;
    }

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
