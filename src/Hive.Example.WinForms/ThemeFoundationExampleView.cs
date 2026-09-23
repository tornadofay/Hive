using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed class ThemeFoundationExampleView : UserControl
{
    private readonly IHiveThemeManager _themeManager;
    private readonly Label _themeState;
    private readonly Label _description;
    private readonly Panel _preview;
    private readonly Label _previewTitle;
    private readonly Label _previewText;
    private Font _previewTitleFont;
    private readonly HiveButton _lightButton;
    private readonly HiveButton _darkButton;
    private readonly HiveButton _systemButton;

    public ThemeFoundationExampleView(IHiveThemeManager themeManager)
    {
        ArgumentNullException.ThrowIfNull(themeManager);

        _themeManager = themeManager;
        _previewTitleFont = new Font(
            themeManager.Theme.Typography.FontFamily,
            11f,
            FontStyle.Bold);

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;
        AutoScroll = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            AutoSize = true
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _description = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(860, 72),
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Text = "Light, Dark, and System modes are applied through Hive's semantic theme contract. The sample below shows surface, text, border, and interactive-state changes."
        };

        var modeButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            Margin = new Padding(0, 18, 0, 0),
            Padding = Padding.Empty
        };

        _lightButton = CreateThemeButton("Light", HiveThemeMode.Light);
        _darkButton = CreateThemeButton("Dark", HiveThemeMode.Dark);
        _systemButton = CreateThemeButton("System", HiveThemeMode.System);

        modeButtons.Controls.Add(_lightButton);
        modeButtons.Controls.Add(_darkButton);
        modeButtons.Controls.Add(_systemButton);

        _themeState = new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 14, 0, 0),
            Padding = Padding.Empty
        };

        _preview = new Panel
        {
            Dock = DockStyle.Top,
            Height = 190,
            Margin = new Padding(0, 18, 0, 0),
            Padding = new Padding(20)
        };

        var previewLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        previewLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        previewLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _previewTitle = new Label
        {
            AutoSize = true,
            Font = _previewTitleFont,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Text = "Surface hierarchy"
        };

        _previewText = new Label
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 0, 0),
            Padding = Padding.Empty,
            Text = "Elevated surfaces, borders, muted text, accent colors, selection, hover, focus, and disabled states are all driven by the same theme definition."
        };

        var previewButton = new HiveButton
        {
            Text = "Interactive state",
            Style = HiveButtonStyle.Secondary,
            Width = 150,
            Height = 36,
            Margin = new Padding(0, 12, 0, 0)
        };

        previewLayout.Controls.Add(_previewTitle, 0, 0);
        previewLayout.Controls.Add(_previewText, 0, 1);
        previewLayout.Controls.Add(previewButton, 0, 2);
        _preview.Controls.Add(previewLayout);

        root.Controls.Add(_description, 0, 0);
        root.Controls.Add(modeButtons, 0, 1);
        root.Controls.Add(_themeState, 0, 2);
        root.Controls.Add(_preview, 0, 3);

        Controls.Add(root);

        _themeManager.ThemeChanged += ThemeManagerOnChanged;
        ApplyTheme();
        UpdateThemeState();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _themeManager.ThemeChanged -= ThemeManagerOnChanged;

        base.Dispose(disposing);

        if (disposing)
            _previewTitleFont.Dispose();
    }

    private HiveButton CreateThemeButton(string text, HiveThemeMode mode)
    {
        var button = new HiveButton
        {
            Text = text,
            Style = HiveButtonStyle.Secondary,
            Width = 104,
            Height = 36,
            Margin = new Padding(0, 0, 8, 0)
        };

        button.AccessibleName = $"{text} theme mode";
        button.AccessibleDescription = $"Switch the application theme to {text} mode.";

        button.Click += (_, _) =>
        {
            _themeManager.SetMode(mode);
            UpdateThemeState();
        };

        return button;
    }

    private void ThemeManagerOnChanged(object? sender, EventArgs e)
    {
        // HiveThemeManager already applies the theme recursively to this view.
        // This handler only updates state that is specific to this example.
        UpdateThemeState();
    }

    private void ApplyTypography(HiveThemeDefinition theme)
    {
        var family = theme.Typography.FontFamily;

        if (string.Equals(
                _previewTitleFont.FontFamily.Name,
                family,
                StringComparison.Ordinal))
        {
            return;
        }

        var previous = _previewTitleFont;
        _previewTitleFont = new Font(family, 11f, FontStyle.Bold);
        _previewTitle.Font = _previewTitleFont;
        previous.Dispose();
    }

    private void ApplyTheme()
    {
        var theme = _themeManager.Theme;
        ApplyTypography(theme);

        BackColor = theme.Palette.Surface;
        ForeColor = theme.Palette.Text;
        _description.ForeColor = theme.Palette.Text;
        _themeState.ForeColor = theme.Palette.MutedText;
        _preview.BackColor = theme.Palette.ElevatedSurface;
        _previewTitle.ForeColor = theme.Palette.Text;
        _previewText.ForeColor = theme.Palette.MutedText;
    }

    private void UpdateThemeState()
    {
        _themeState.Text =
            $"Selected: {_themeManager.Mode}    Effective: {_themeManager.Theme.Mode}";

        _lightButton.Style =
            _themeManager.Mode == HiveThemeMode.Light
                ? HiveButtonStyle.NavigationSelected
                : HiveButtonStyle.Secondary;
        _darkButton.Style =
            _themeManager.Mode == HiveThemeMode.Dark
                ? HiveButtonStyle.NavigationSelected
                : HiveButtonStyle.Secondary;
        _systemButton.Style =
            _themeManager.Mode == HiveThemeMode.System
                ? HiveButtonStyle.NavigationSelected
                : HiveButtonStyle.Secondary;
    }
}
