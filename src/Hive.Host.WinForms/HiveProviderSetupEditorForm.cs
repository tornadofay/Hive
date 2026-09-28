using System.Drawing;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms;

internal sealed class HiveProviderSetupEditorForm : HiveForm
{
    private readonly BuiltInProviderDefinition? _existingCatalog;
    private readonly ComboBox _providerComboBox;
    private readonly TextBox _credentialTextBox;
    private readonly Label _detailsLabel;
    private readonly HiveButton _saveButton;
    private readonly HiveButton _cancelButton;
    private readonly IHiveThemeManager _themeManager;
    private readonly IHiveExampleOutput? _output;

    public HiveProviderSetupEditorForm(
        Provider? existing,
        IHiveThemeManager themeManager,
        IHiveExampleOutput? output = null)
        : base(
            existing is null ? "Add Provider" : "Edit Provider Credential",
            existing is null
                ? "Select a built-in provider and enter its protected credential."
                : "Replace the protected credential for this configured provider.",
            new Size(660, 400),
            new Size(580, 340),
            themeManager)
    {
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _output = output;
        _existingCatalog = existing is null
            ? null
            : BuiltInProviderCatalog.Find(existing.Key);

        ConfigureHeader(
            allowMove: true,
            allowClose: true,
            allowMinimize: false,
            allowMaximize: false,
            allowHelp: false,
            allowThemeToggle: true);

        SetBodyPadding(new Padding(20));

        var editor = new HiveEditorLayout();

        _providerComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Height = 32,
            IntegralHeight = false
        };

        foreach (var definition in BuiltInProviderCatalog.All)
            _providerComboBox.Items.Add(new ProviderChoice(definition));


        _credentialTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Height = 32,
            BorderStyle = BorderStyle.FixedSingle,
            UseSystemPasswordChar = true,
            PlaceholderText = "API key"
        };

        _detailsLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 72,
            Margin = new Padding(0, 4, 0, 0),
            Padding = new Padding(0, 4, 0, 4),
            AutoEllipsis = true
        };

        if (existing is null)
        {
            if (_providerComboBox.Items.Count > 0)
                _providerComboBox.SelectedIndex = 0;
        }
        else if (_existingCatalog is not null)
        {
            for (var index = 0; index < _providerComboBox.Items.Count; index++)
            {
                if (_providerComboBox.Items[index] is ProviderChoice choice &&
                    choice.Value.Key == _existingCatalog.Key)
                {
                    _providerComboBox.SelectedIndex = index;
                    break;
                }
            }

            _providerComboBox.Enabled = false;
        }
        else
        {
            _providerComboBox.Items.Add(
                new ProviderChoice(
                    new BuiltInProviderDefinition(
                        existing.Key,
                        existing.DisplayName,
                        existing.TransportKind,
                        BuiltInProviderCredentialKind.ApiKey,
                        null,
                        normalOnboardingSupported: false,
                        "This provider is managed through Advanced Configuration.")));
            _providerComboBox.SelectedIndex = _providerComboBox.Items.Count - 1;
            _providerComboBox.Enabled = false;
        }

        editor.AddField(
            "Provider",
            "Built-in provider identity. Provider identity and transport are not editable in the normal setup surface.",
            _providerComboBox);

        editor.AddField(
            "Credential",
            "The credential is stored through Hive's Secret Store and is never returned to the Provider Settings list.",
            _credentialTextBox);

        editor.AddField(
            "Details",
            "Provider onboarding information.",
            _detailsLabel,
            90);

        _saveButton = editor.AddActionButton(
            existing is null ? "Add Provider" : "Replace Credential",
            HiveButtonStyle.Primary,
            136);

        _cancelButton = editor.AddActionButton(
            "Cancel",
            HiveButtonStyle.Secondary,
            96);

        _cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        _saveButton.Click += (_, _) => Save();

        _providerComboBox.SelectedIndexChanged += ProviderComboBoxOnSelectedIndexChanged;

        AcceptButton = _saveButton;
        CancelButton = _cancelButton;

        BodyPanel.Controls.Add(editor);
        ThemeManager.Apply(BodyPanel);
        UpdateDetails();
    }

    public string ProviderKey =>
        (_providerComboBox.SelectedItem as ProviderChoice)?.Value.Key ?? string.Empty;

    public string Credential => _credentialTextBox.Text;

    private void ProviderComboBoxOnSelectedIndexChanged(
        object? sender,
        EventArgs e) => UpdateDetails();

    private void UpdateDetails()
    {
        var definition = (_providerComboBox.SelectedItem as ProviderChoice)?.Value;

        if (definition is null)
        {
            _detailsLabel.Text = "Select a provider.";
            _saveButton.Enabled = false;
            return;
        }

        if (!definition.NormalOnboardingSupported)
        {
            _detailsLabel.Text =
                definition.OnboardingNote ??
                "This provider requires Advanced Configuration.";
            _saveButton.Enabled = false;
            return;
        }

        _credentialTextBox.Enabled =
            definition.CredentialKind != BuiltInProviderCredentialKind.None;

        if (definition.CredentialKind == BuiltInProviderCredentialKind.None)
        {
            _credentialTextBox.Clear();
            _credentialTextBox.PlaceholderText = "No credential required";
            _detailsLabel.Text =
                $"Endpoint: {definition.DefaultEndpoint}\r\nThis provider uses local/default configuration.";
        }
        else
        {
            _credentialTextBox.PlaceholderText = "API key";
            _detailsLabel.Text =
                $"Endpoint: {definition.DefaultEndpoint}\r\nThe API key is stored as protected secret material.";
        }

        _saveButton.Enabled = true;
    }

    private void Save()
    {
        try
        {
            var definition = (_providerComboBox.SelectedItem as ProviderChoice)?.Value
                ?? throw new InvalidOperationException("A provider must be selected.");

            if (!definition.NormalOnboardingSupported)
                throw new InvalidOperationException(
                    definition.OnboardingNote ??
                    "This provider must be configured through Advanced Configuration.");

            if (definition.RequiresCredential &&
                string.IsNullOrWhiteSpace(_credentialTextBox.Text))
            {
                throw new InvalidOperationException("An API key is required for this provider.");
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            HiveUiErrorReporter.Report(
                this,
                exception,
                "Provider",
                "The provider configuration could not be saved.",
                _output,
                _themeManager);
        }
    }

    private sealed record ProviderChoice(BuiltInProviderDefinition Value)
    {
        public override string ToString() => Value.DisplayName;
    }
}
