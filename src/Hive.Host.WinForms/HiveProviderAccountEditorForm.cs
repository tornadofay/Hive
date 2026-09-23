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

        _providerTextBox = CreateReadOnlyTextBox(_provider.DisplayName);
        _keyTextBox = CreateTextBox();
        _nameTextBox = CreateTextBox();
        _externalAccountTextBox = CreateTextBox();
        _credentialTextBox = CreateTextBox();
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
            _keyTextBox.ReadOnly = true;
            _keyTextBox.BackColor = themeManager.Theme.Palette.DisabledBackground;
            _keyTextBox.ForeColor = themeManager.Theme.Palette.DisabledText;
        }

        editor.AddField(
            "Provider",
            "Owning Provider resource. Provider ownership is fixed after account creation.",
            _providerTextBox);

        editor.AddField(
            "Key",
            "Stable ProviderAccount identity. It cannot be changed after creation.",
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
            "API credential",
            "Stored in Hive Secret Store. The secret material is never persisted on ProviderAccount itself and is never displayed after save.",
            credentialPanel,
            86);

        var cancel = editor.AddActionButton(
            "Cancel",
            HiveButtonStyle.Secondary,
            96);
        var save = editor.AddActionButton(
            account is null ? "Create" : "Save",
            HiveButtonStyle.Primary,
            96);

        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        save.Click += (_, _) => Save();

        Controls.Add(editor);
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
            BorderStyle = BorderStyle.FixedSingle
        };

    private static TextBox CreateReadOnlyTextBox(string value)
    {
        var box = CreateTextBox();
        box.Text = value;
        box.ReadOnly = true;
        return box;
    }
}
