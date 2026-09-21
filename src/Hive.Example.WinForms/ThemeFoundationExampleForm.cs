using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed class ThemeFoundationExampleForm : HiveForm
{
    private readonly FlowLayoutPanel _navigation;
    private readonly Panel _content;
    private readonly TableLayoutPanel _contentLayout;
    private readonly FlowLayoutPanel _pageBody;
    private readonly Label _pageTitle;
    private readonly Label _pageDescription;
    private readonly IHiveThemeManager _themeManager;
    private readonly Font _pageTitleFont;

    public ThemeFoundationExampleForm()
        : base(
            "Hive Example",
            "WinForms UI/UX foundation",
            new Size(1100, 700),
            new Size(900, 560))
    {
        _themeManager = ThemeManager;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;

        SetBodyPadding(Padding.Empty);
        _themeManager.ThemeChanged += ThemeManagerOnChanged;

        _navigation = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            Width = 190,
            Padding = new Padding(12, 18, 12, 12),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Margin = Padding.Empty
        };

        _content = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28, 24, 28, 24)
        };

        _contentLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        _pageTitleFont = new Font("Segoe UI", 16f, FontStyle.Bold);
        _pageTitle = new Label
        {
            AutoSize = true,
            Font = _pageTitleFont,
            Margin = Padding.Empty
        };

        _pageDescription = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(800, 80),
            Margin = new Padding(0, 8, 0, 20)
        };

        _pageBody = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        _contentLayout.Controls.Add(_pageTitle, 0, 0);
        _contentLayout.Controls.Add(_pageDescription, 0, 1);
        _contentLayout.Controls.Add(_pageBody, 0, 2);

        _content.Controls.Add(_contentLayout);
        BodyPanel.Controls.Add(_content);
        BodyPanel.Controls.Add(_navigation);

        AddNavigation("Theme", ShowTheme);
        AddNavigation("Controls", ShowControls);
        AddNavigation("Dialogs", ShowDialogs);

        ApplyExampleTheme(Theme);
        ShowTheme();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _themeManager.ThemeChanged -= ThemeManagerOnChanged;
            _pageTitleFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private void ThemeManagerOnChanged(object? sender, EventArgs e) =>
        ApplyExampleTheme(_themeManager.Theme);

    private void ApplyExampleTheme(HiveThemeDefinition theme)
    {
        _navigation.BackColor = theme.VisualStates.NavigationBackground;
        _content.BackColor = theme.Palette.Surface;
        _contentLayout.BackColor = theme.Palette.Surface;
        _pageBody.BackColor = theme.Palette.Surface;
    }

    private void AddNavigation(string text, Action action)
    {
        var button = new HiveButton
        {
            Text = text,
            Style = HiveButtonStyle.Navigation,
            Width = 166,
            Height = 42,
            Margin = new Padding(0, 0, 0, 8)
        };

        button.Click += (_, _) => action();
        _navigation.Controls.Add(button);
    }

    private void ShowTheme()
    {
        SetPage(
            "Theme",
            "Light, Dark, and System modes use Hive-owned semantic tokens and can be switched at runtime.");

        ClearPageActions();

        AddActionButton("Light", HiveThemeMode.Light);
        AddActionButton("Dark", HiveThemeMode.Dark);
        AddActionButton("System", HiveThemeMode.System);

        var current = CreateBodyLabel();
        current.Name = "ThemeState";
        current.Margin = new Padding(0, 18, 0, 0);
        _pageBody.Controls.Add(current);
        UpdateThemeState(current);
    }

    private void ShowControls()
    {
        SetPage(
            "Controls",
            "Hive-prefixed controls exist only where Hive adds a consumer-facing contract or styling beyond ordinary WinForms.");

        ClearPageActions();

        var input = new TextBox
        {
            Width = 320,
            Height = 28,
            Text = "Native WinForms TextBox"
        };

        var checkBox = new CheckBox
        {
            AutoSize = true,
            Text = "Native WinForms CheckBox"
        };

        var disabled = new CheckBox
        {
            AutoSize = true,
            Enabled = false,
            Text = "Disabled control state"
        };

        var primary = new HiveButton
        {
            Text = "HiveButton — Primary",
            Style = HiveButtonStyle.Primary,
            Width = 190,
            Height = 40,
            Margin = new Padding(0, 10, 0, 0)
        };

        var secondary = new HiveButton
        {
            Text = "HiveButton — Secondary",
            Style = HiveButtonStyle.Secondary,
            Width = 190,
            Height = 40,
            Margin = new Padding(0, 8, 0, 0)
        };

        _pageBody.Controls.Add(input);
        _pageBody.Controls.Add(checkBox);
        _pageBody.Controls.Add(disabled);
        _pageBody.Controls.Add(primary);
        _pageBody.Controls.Add(secondary);
    }

    private void ShowDialogs()
    {
        SetPage(
            "Dialogs",
            "HiveMessageBox provides semantic Information, Success, Warning, Error, and Question dialogs with optional technical details.");

        ClearPageActions();

        AddDialogButton(
            "Information",
            HiveMessageType.Information,
            "The information operation completed.");
        AddDialogButton(
            "Success",
            HiveMessageType.Success,
            "The operation completed successfully.");
        AddDialogButton(
            "Warning",
            HiveMessageType.Warning,
            "Review the current state before continuing.");
        AddDialogButton(
            "Error",
            HiveMessageType.Error,
            "The operation could not be completed.");
        AddDialogButton(
            "Question",
            HiveMessageType.Question,
            "Continue with this operation?",
            MessageBoxButtons.YesNo);

        var details = new HiveButton
        {
            Text = "Error with technical details",
            Style = HiveButtonStyle.Secondary,
            Width = 220,
            Height = 40,
            Margin = new Padding(0, 12, 0, 0)
        };
        details.Click += (_, _) =>
            HiveMessageBox.Show(
                this,
                new HiveMessageOptions(
                    "Operation failed",
                    "The operation could not be completed.",
                    HiveMessageType.Error,
                    MessageBoxButtons.OK,
                    "Example technical details\r\nCode: UI-0001\r\nPath: Hive.Example.WinForms",
                    DetailsExpanded: true),
                _themeManager);

        _pageBody.Controls.Add(details);
    }

    private void AddThemeButton(string text, HiveThemeMode mode)
    {
        var button = new HiveButton
        {
            Text = text,
            Style = HiveButtonStyle.Secondary,
            Width = 110,
            Height = 38,
            Margin = new Padding(0, 0, 0, 8)
        };

        button.Click += (_, _) =>
        {
            _themeManager.SetMode(mode);
            if (_pageBody.Controls["ThemeState"] is Label state)
                UpdateThemeState(state);
        };

        _pageBody.Controls.Add(button);
    }

    private void AddActionButton(string text, HiveThemeMode mode) =>
        AddThemeButton(text, mode);

    private void AddDialogButton(
        string text,
        HiveMessageType type,
        string message,
        MessageBoxButtons buttons = MessageBoxButtons.OK)
    {
        var button = new HiveButton
        {
            Text = text,
            Style = HiveButtonStyle.Secondary,
            Width = 190,
            Height = 40,
            Margin = new Padding(0, 0, 0, 8)
        };

        button.Click += (_, _) =>
            HiveMessageBox.Show(
                this,
                new HiveMessageOptions(
                    text,
                    message,
                    type,
                    buttons),
                _themeManager);

        _pageBody.Controls.Add(button);
    }

    private void SetPage(string title, string description)
    {
        _pageTitle.Text = title;
        _pageDescription.Text = description;
    }

    private void ClearPageActions()
    {
        while (_pageBody.Controls.Count > 0)
        {
            var last = _pageBody.Controls[_pageBody.Controls.Count - 1];
            _pageBody.Controls.RemoveAt(_pageBody.Controls.Count - 1);
            last.Dispose();
        }
    }

    private void UpdateThemeState(Label label) =>
        label.Text = $"Selected: {_themeManager.Mode}\r\nEffective: {_themeManager.Theme.Mode}";

    private static Label CreateBodyLabel(string? text = null) =>
        new()
        {
            AutoSize = true,
            Text = text ?? string.Empty,
            MaximumSize = new Size(700, 120)
        };
}
