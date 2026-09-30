using System.Drawing;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms;

internal sealed class HiveProviderEditorForm : HiveForm
{
    private readonly Provider? _existing;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveExampleOutput? _output;
    private readonly TextBox _keyTextBox;
    private readonly TextBox _nameTextBox;
    private readonly ComboBox _transportComboBox;
    private readonly HiveButton _saveButton;
    private readonly HiveButton _cancelButton;

    public HiveProviderEditorForm(
        Provider? provider,
        ResourceAccessContext accessContext,
        IHiveThemeManager themeManager,
        IHiveExampleOutput? output = null)
        : base(
            provider is null ? "New Provider" : "Edit Provider",
            "Provider resource identity and transport configuration",
            new Size(700, 440),
            new Size(600, 380),
            themeManager)
    {
        _existing = provider;
        _accessContext = accessContext
            ?? throw new ArgumentNullException(nameof(accessContext));
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

        _keyTextBox = CreateTextBox();
        _keyTextBox.PlaceholderText = "e.g. openrouter";
        _nameTextBox = CreateTextBox();
        _nameTextBox.PlaceholderText = "e.g. OpenRouter";
        _transportComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDown,
            AutoCompleteMode = AutoCompleteMode.SuggestAppend,
            AutoCompleteSource = AutoCompleteSource.ListItems
        };

        if (_existing is not null)
        {
            _transportComboBox.Items.Add(_existing.TransportKind);
            _transportComboBox.Text = _existing.TransportKind;
        }
        else
        {
            _transportComboBox.Items.Add("openai-compatible");
            _transportComboBox.Text = "openai-compatible";
        }

        _keyTextBox.Text = _existing?.Key ?? string.Empty;
        _nameTextBox.Text = _existing?.DisplayName ?? string.Empty;

        if (_existing is not null)
        {
            SetReadOnlyVisualState(_keyTextBox, themeManager);
        }

        editor.AddField(
            "Resource key",
            "Stable internal Provider identifier. This is NOT an API key or credential. It becomes read-only after creation.",
            _keyTextBox);

        editor.AddField(
            "Display name",
            "Human-readable provider name shown throughout Hive Settings.",
            _nameTextBox);

        editor.AddField(
            "Transport",
            "Transport kind consumed by the provider integration boundary, for example openai-compatible.",
            _transportComboBox,
            62);

        _saveButton = editor.AddActionButton(
            provider is null ? "Create" : "Save",
            HiveButtonStyle.Primary,
            96);

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

        AcceptButton = _saveButton;
        CancelButton = _cancelButton;

        BodyPanel.Controls.Add(editor);
        ThemeManager.Apply(BodyPanel);
    }

    public Provider? Definition { get; private set; }

    private void Save()
    {
        try
        {
            var key = _keyTextBox.Text.Trim();
            var name = _nameTextBox.Text.Trim();
            var transport = _transportComboBox.Text.Trim();

            if (_existing is null)
            {
                Definition = new Provider(
                    HiveSettingsResourceFactory.CreateEnvelope(
                        ResourceKind.Provider,
                        ProviderId.New(),
                        _accessContext),
                    key,
                    name,
                    transport);
            }
            else
            {
                Definition = _existing
                    .WithDisplayName(name)
                    .WithTransportKind(transport);
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
                "The Provider could not be saved.",
                _output,
                ThemeManager);
        }
    }

    private static TextBox CreateTextBox() =>
        new()
        {
            Dock = DockStyle.Fill,
            Height = 32,
            BorderStyle = BorderStyle.FixedSingle
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
