using System.Drawing;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms;

internal sealed class HiveProviderAccountEditorForm : HiveForm
{
    private readonly ProviderAccount? _existing;
    private readonly Provider _provider;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveExampleOutput? _output;
    private readonly TextBox _providerTextBox;
    private readonly TextBox _keyTextBox;
    private readonly TextBox _nameTextBox;
    private readonly TextBox _externalAccountTextBox;
    private readonly TextBox _credentialTextBox;
    private readonly Label _credentialStatus;

    public HiveProviderAccountEditorForm(
        ProviderAccount? account,
        Provider provider,
        ResourceAccessContext accessContext,
        IHiveThemeManager themeManager,
        IHiveExampleOutput? output = null)
        : base(
            account is null ? "New Provider Account" : "Edit Provider Account",
            "Durable provider credential/resource record",
            new Size(760, 520),
            new Size(640, 450),
            themeManager)
    {
        _existing = account;
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
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
        _keyTextBox = CreateTextBox();
        _keyTextBox.PlaceholderText = "e.g. personal";
        _nameTextBox = CreateTextBox();
        _nameTextBox.PlaceholderText = "e.g. Personal OpenRouter";
        _externalAccountTextBox = CreateTextBox();
        _externalAccountTextBox.PlaceholderText = "Optional vendor/project/account ID";
        _credentialTextBox = CreateTextBox();
        _credentialTextBox.PlaceholderText = "Paste API key / token here";
        _credentialTextBox.UseSystemPasswordChar = true;
        _credentialStatus = new Label
        {
            AutoSize = false,
            Height = 22,
            Dock = DockStyle.Bottom
        };

        _keyTextBox.Text = account?.Key ?? string.Empty;
        _nameTextBox.Text = account?.DisplayName ?? string.Empty;
        _externalAccountTextBox.Text = account?.ExternalAccountId ?? string.Empty;

        _credentialStatus.Text =
            account?.CredentialSecret is null
                ? "Saved credential: not configured. Enter an API key to create one."
                : "Saved credential: configured. Leave the field blank to keep it.";

        if (account is not null)
        {
            SetReadOnlyVisualState(_keyTextBox, themeManager);
        }

        editor.AddField(
            "Provider",
            "Fixed parent Provider. This relationship cannot be changed after account creation.",
            _providerTextBox);

        editor.AddField(
            "Resource key",
            "Stable internal Provider Account identifier. This is NOT an API key. It becomes read-only after creation.",
            _keyTextBox);

        editor.AddField(
            "Display name",
            "Human-readable account/resource name.",
            _nameTextBox);

        editor.AddField(
            "External account",
            "Optional vendor, project, subscription, or account identifier.",
            _externalAccountTextBox);

        var credentialPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = Padding.Empty
        };
        _credentialTextBox.Dock = DockStyle.Top;
        _credentialTextBox.Height = 32;
        credentialPanel.Controls.Add(_credentialTextBox);
        credentialPanel.Controls.Add(_credentialStatus);

        editor.AddField(
            "API key / credential",
            "This is the actual secret used for provider access. Stored in Hive Secret Store and never displayed after save. Leave blank while editing to keep the existing credential.",
            credentialPanel,
            86);

        var save = editor.AddActionButton(
            account is null ? "Create" : "Save",
            HiveButtonStyle.Primary,
            96);
        var cancel = editor.AddActionButton(
            "Cancel",
            HiveButtonStyle.Secondary,
            96);

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
    }

    public ProviderAccount? Definition { get; private set; }

    public string? CredentialText =>
        string.IsNullOrWhiteSpace(_credentialTextBox.Text)
            ? null
            : _credentialTextBox.Text;

    public void ClearCredential() => _credentialTextBox.Clear();

    private void Save()
    {
        try
        {
            var key = _keyTextBox.Text.Trim();
            var name = _nameTextBox.Text.Trim();
            var externalAccount = string.IsNullOrWhiteSpace(_externalAccountTextBox.Text)
                ? null
                : _externalAccountTextBox.Text.Trim();

            Definition = _existing is null
                ? new ProviderAccount(
                    HiveSettingsResourceFactory.CreateEnvelope(
                        ResourceKind.ProviderAccount,
                        ProviderAccountId.New(),
                        _accessContext),
                    _provider.Id,
                    key,
                    name,
                    externalAccount)
                : _existing
                    .WithDisplayName(name)
                    .WithExternalAccountId(externalAccount);

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            HiveUiErrorReporter.Report(
                this,
                exception,
                "Provider Account",
                "The Provider Account could not be saved.",
                _output,
                ThemeManager);
        }
    }

    private static TextBox CreateTextBox() =>
        new()
        {
            Dock = DockStyle.Fill,
            Height = 32,
            AutoSize = false,
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
