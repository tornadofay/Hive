using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed class ThemeFoundationExampleForm : HiveForm
{
    private readonly Panel _navigation;
    private readonly Panel _content;
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

        BodyPanel.Padding = Padding.Empty;

        _navigation = new Panel
        {
            Dock = DockStyle.Left,
            Width = 190,
            Padding = new Padding(12, 18, 12, 12)
        };

        _content = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28, 24, 28, 24)
        };

        _pageTitleFont = new Font("Segoe UI", 16f, FontStyle.Bold);
        _pageTitle = new Label
        {
            AutoSize = true,
            Font = _pageTitleFont
        };

        _pageDescription = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(700, 80),
            Margin = new Padding(0, 8, 0, 20)
        };

        BodyPanel.Controls.Add(_content);
        BodyPanel.Controls.Add(_navigation);

        AddNavigation("Overview", ShowOverview);
        AddNavigation("Theme", ShowTheme);
        AddNavigation("Controls", ShowControls);
        AddNavigation("Dialogs", ShowDialogs);

        BuildContent();
        ShowOverview();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _pageTitleFont.Dispose();

        base.Dispose(disposing);
    }

    private void BuildContent()
    {
        _content.Controls.Add(_pageDescription);
        _content.Controls.Add(_pageTitle);
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

    private void ShowOverview()
    {
        SetPage(
            "UI foundation",
            "A production-oriented WinForms shell inspired by the useful parts of HAgent: a custom window header, compact navigation, semantic themes, reusable controls, and a Hive-owned message dialog.");

        ClearPageActions();

        var status = CreateBodyLabel(
            "Renderer boundary: ReaLTaiizor remains inside Hive.Host.WinForms.UI.
" +
            "Consumer forms use Hive-owned contracts only.");
        _content.Controls.Add(status);
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
        _content.Controls.Add(current);
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
            Margin = new Padding(0, 16, 0, 8)
        };

        var secondary = new HiveButton
        {
            Text = "HiveButton — Secondary",
            Style = HiveButtonStyle.Secondary,
            Width = 190,
            Height = 40
        };

        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Dock = DockStyle.Top
        };

        panel.Controls.Add(input);
        panel.Controls.Add(checkBox);
        panel.Controls.Add(disabled);
        panel.Controls.Add(primary);
        panel.Controls.Add(secondary);

        _content.Controls.Add(panel);
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

        _content.Controls.Add(details);
    }

    private void AddThemeButton(string text, HiveThemeMode mode)
    {
        var button = new HiveButton
        {
            Text = text,
            Style = HiveButtonStyle.Secondary,
            Width = 110,
            Height = 38,
            Margin = new Padding(0, 0, 8, 0)
        };

        button.Click += (_, _) =>
        {
            _themeManager.SetMode(mode);
            if (_content.Controls["ThemeState"] is Label state)
                UpdateThemeState(state);
        };

        _content.Controls.Add(button);
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

        _content.Controls.Add(button);
    }

    private void SetPage(string title, string description)
    {
        _pageTitle.Text = title;
        _pageDescription.Text = description;
    }

    private void ClearPageActions()
    {
        while (_content.Controls.Count > 2)
        {
            var last = _content.Controls[_content.Controls.Count - 1];
            _content.Controls.RemoveAt(_content.Controls.Count - 1);
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
            MaximumSize = new Size(700, 120),
            Margin = new Padding(0, 10, 0, 0)
        };
}
