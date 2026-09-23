using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed class ExampleConfigurationExampleView : UserControl
{
    private readonly IHiveThemeManager _themeManager;
    private readonly Label _title;
    private readonly Label _intro;
    private readonly TableLayoutPanel _providerFlow;
    private readonly TableLayoutPanel _settingsFlow;
    private readonly TableLayoutPanel _futureText;
    private readonly HiveButton _openSettingsButton;
    private Font _titleFont;
    private Font _sectionFont;
    private Font _bodyFont;

    public ExampleConfigurationExampleView(
        IHiveThemeManager themeManager)
    {
        _themeManager = themeManager
            ?? throw new ArgumentNullException(nameof(themeManager));

        Dock = DockStyle.Fill;
        AutoScroll = true;
        Padding = new Padding(2, 2, 2, 18);

        var family = themeManager.Theme.Typography.FontFamily;
        _titleFont = new Font(family, 19f, FontStyle.Bold);
        _sectionFont = new Font(family, 10.5f, FontStyle.Bold);
        _bodyFont = new Font(family, 9.25f);

        _title = new Label
        {
            Dock = DockStyle.Top,
            Height = 40,
            Font = _titleFont,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Text = "Example Configuration"
        };

        _intro = CreateBodyLabel(
            "This page explains the normal Hive configuration model. Use the button below to open the real Hive Settings center used by the host. The page itself does not own or duplicate Hive configuration.");

        _providerFlow = CreateSection(
            "Provider configuration",
            "Start with a provider such as OpenAI, Groq, NVIDIA, or another supported integration. In the normal setup flow you configure the credential and the model/endpoint you want to use. Hive keeps the underlying Provider, ProviderAccount, and ExecutionTarget resources separate so advanced hosts can manage multiple credentials, models, endpoints, deployments, and capability-specific targets.");

        _settingsFlow = CreateSection(
            "Advanced resources",
            "Accounts / Credentials and Execution Targets are administrative resources, not provider login screens. An API key is stored as a Hive Secret Store reference; you do not create or sign into a separate Hive user account for every provider. The AgentDefinition then references the selected ExecutionTarget rather than duplicating provider or credential details.");

        _futureText = CreateSection(
            "Configuration grows with Hive",
            "Persistence is a single global Hive configuration rather than a CRUD resource list. Future Hive-owned configuration such as Tools, Policy / Permissions, Runtime / Execution Defaults, Cognition, Knowledge, Skills, and Memory will appear in this same Settings center only when their authoritative contracts exist. Empty placeholder pages are not created just to fill the navigation.");

        _openSettingsButton = new HiveButton
        {
            Text = "Open Hive Settings",
            Style = HiveButtonStyle.Primary,
            Width = 172,
            Height = 38,
            Margin = new Padding(0, 18, 0, 18)
        };
        _openSettingsButton.Click += (_, _) => OpenSettings();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 6,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        root.Controls.Add(_title, 0, 0);
        root.Controls.Add(_intro, 0, 1);
        root.Controls.Add(_openSettingsButton, 0, 2);
        root.Controls.Add(_providerFlow, 0, 3);
        root.Controls.Add(_settingsFlow, 0, 4);
        root.Controls.Add(_futureText, 0, 5);

        Controls.Add(root);

        _themeManager.ThemeChanged += ThemeManagerOnChanged;
        ApplyTheme(_themeManager.Theme);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _themeManager.ThemeChanged -= ThemeManagerOnChanged;
            _titleFont.Dispose();
            _sectionFont.Dispose();
            _bodyFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private void OpenSettings()
    {
        if (FindForm() is HiveExampleHostForm host)
        {
            host.OpenHiveSettings();
            return;
        }

        HiveUiErrorReporter.Report(
            FindForm(),
            "Hive Settings can only be opened from the Hive Example Host.",
            "Hive Settings",
            null,
            _themeManager);
    }

    private TableLayoutPanel CreateSection(
        string title,
        string body)
    {
        var section = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 16),
            Padding = new Padding(0, 4, 18, 4)
        };
        section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        section.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        section.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        section.Controls.Add(
            new Label
            {
                Dock = DockStyle.Fill,
                Font = _sectionFont,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                Text = title
            },
            0,
            0);

        section.Controls.Add(
            CreateBodyLabel(body),
            0,
            1);

        return section;
    }

    private Label CreateBodyLabel(string text) =>
        new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Font = _bodyFont,
            Margin = new Padding(0, 5, 0, 0),
            Padding = Padding.Empty,
            Text = text,
            MaximumSize = new Size(920, 0)
        };

    private void ThemeManagerOnChanged(object? sender, EventArgs e) =>
        ApplyTheme(_themeManager.Theme);

    private void ApplyTypography(HiveThemeDefinition theme)
    {
        var family = theme.Typography.FontFamily;

        if (string.Equals(
                _bodyFont.FontFamily.Name,
                family,
                StringComparison.Ordinal))
        {
            return;
        }

        var previousTitle = _titleFont;
        var previousSection = _sectionFont;
        var previousBody = _bodyFont;

        _titleFont = new Font(family, 19f, FontStyle.Bold);
        _sectionFont = new Font(family, 10.5f, FontStyle.Bold);
        _bodyFont = new Font(family, 9.25f);

        _title.Font = _titleFont;
        _intro.Font = _bodyFont;

        foreach (var section in new[] { _providerFlow, _settingsFlow, _futureText })
        {
            if (section.Controls.Count == 2)
            {
                section.Controls[0].Font = _sectionFont;

                if (section.Controls[1] is Label body)
                    body.Font = _bodyFont;
            }
        }

        previousTitle.Dispose();
        previousSection.Dispose();
        previousBody.Dispose();
    }

    private void ApplyTheme(HiveThemeDefinition theme)
    {
        ApplyTypography(theme);

        BackColor = theme.Palette.Surface;
        _title.ForeColor = theme.Palette.Text;
        _intro.ForeColor = theme.Palette.MutedText;
        _providerFlow.BackColor = theme.Palette.ElevatedSurface;
        _settingsFlow.BackColor = theme.Palette.ElevatedSurface;
        _futureText.BackColor = theme.Palette.ElevatedSurface;

        foreach (var section in new[] { _providerFlow, _settingsFlow, _futureText })
        {
            if (section.Controls.Count == 2)
            {
                section.Controls[0].ForeColor = theme.Palette.Text;

                if (section.Controls[1] is Label body)
                    body.ForeColor = theme.Palette.MutedText;
            }
        }
    }
}
