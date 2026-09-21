using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed partial class ThemeFoundationExampleView : UserControl
{
    private readonly IHiveThemeManager _themeManager;
    private readonly FlowLayoutPanel _navigation;
    private readonly Panel _content;
    private readonly TableLayoutPanel _contentLayout;
    private readonly Panel _pageBody;
    private readonly FlowLayoutPanel _themePage;
    private readonly FlowLayoutPanel _controlsPage;
    private readonly FlowLayoutPanel _dialogsPage;
    private readonly Label _navigationTitle;
    private readonly Label _navigationDescription;
    private readonly Label _pageTitle;
    private readonly Label _pageDescription;
    private readonly Label _themeState;
    private readonly Font _navigationTitleFont;
    private readonly Font _navigationDescriptionFont;
    private readonly Font _pageTitleFont;
    private readonly Panel _bodyPanel;

    private HiveButton? _selectedNavigationButton;

    public ThemeFoundationExampleView(IHiveThemeManager themeManager)
    {
        ArgumentNullException.ThrowIfNull(themeManager);

        _themeManager = themeManager;
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _bodyPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = Padding.Empty
        };

        _navigation = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            Width = 184,
            Padding = new Padding(14, 16, 12, 12),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Margin = Padding.Empty
        };

        _content = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(26, 22, 26, 22)
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

        _navigationTitleFont = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
        _navigationDescriptionFont = new Font("Segoe UI", 8.4f);
        _pageTitleFont = new Font("Segoe UI Semibold", 16f, FontStyle.Bold);

        _navigationTitle = new Label
        {
            AutoSize = true,
            Font = _navigationTitleFont,
            Margin = new Padding(2, 0, 0, 2),
            Padding = Padding.Empty,
            Text = "SECTIONS"
        };

        _navigationDescription = new Label
        {
            AutoSize = true,
            Font = _navigationDescriptionFont,
            Margin = new Padding(2, 0, 0, 14),
            Padding = Padding.Empty,
            Text = "Shared UI foundation"
        };

        _pageTitle = new Label
        {
            AutoSize = true,
            Font = _pageTitleFont,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        _pageDescription = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(900, 72),
            Margin = new Padding(0, 6, 0, 18)
        };

        _pageBody = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        _themePage = CreatePage();
        _controlsPage = CreatePage();
        _dialogsPage = CreatePage();

        _themeState = CreateBodyLabel();

        _themePage.Controls.Add(CreateBodyLabel(
            "Light, Dark, and System modes use Hive-owned semantic tokens and can be switched at runtime."));
        _themePage.Controls.Add(CreateThemeButton("Light", HiveThemeMode.Light));
        _themePage.Controls.Add(CreateThemeButton("Dark", HiveThemeMode.Dark));
        _themePage.Controls.Add(CreateThemeButton("System", HiveThemeMode.System));
        _themeState.Margin = new Padding(0, 16, 0, 0);
        _themePage.Controls.Add(_themeState);

        _controlsPage.Controls.Add(CreateBodyLabel(
            "Hive-specific controls add only the consumer-facing behavior or styling that ordinary WinForms controls do not provide."));
        BuildControlsPage();
        BuildListCompositionExample();

        _dialogsPage.Controls.Add(CreateBodyLabel(
            "HiveMessageBox provides consistent semantic dialogs, optional technical details, and a predictable button hierarchy."));
        BuildDialogsPage();

        _contentLayout.Controls.Add(_pageTitle, 0, 0);
        _contentLayout.Controls.Add(_pageDescription, 0, 1);
        _contentLayout.Controls.Add(_pageBody, 0, 2);

        _content.Controls.Add(_contentLayout);
        _bodyPanel.Controls.Add(_content);
        _bodyPanel.Controls.Add(_navigation);

        _navigation.Controls.Add(_navigationTitle);
        _navigation.Controls.Add(_navigationDescription);

        Controls.Add(_bodyPanel);

        AddNavigation(
            "Theme",
            _themePage,
            "Theme",
            "Light, Dark, and System modes use semantic theme tokens.",
            selected: true);
        AddNavigation(
            "Controls",
            _controlsPage,
            "Controls",
            "Native WinForms and Hive-specific control states.");
        AddNavigation(
            "Dialogs",
            _dialogsPage,
            "Dialogs",
            "Semantic dialogs and technical error details.");

        _themeManager.ThemeChanged += ThemeManagerOnChanged;
        _themeManager.Apply(_bodyPanel);
        ShowPage(
            _themePage,
            "Theme",
            "Light, Dark, and System modes use Hive-owned semantic tokens and can be switched at runtime.");
        UpdateThemeState();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _themeManager.ThemeChanged -= ThemeManagerOnChanged;

        base.Dispose(disposing);

        if (disposing)
        {
            _navigationTitleFont.Dispose();
            _navigationDescriptionFont.Dispose();
            _pageTitleFont.Dispose();
        }
    }

    private void ThemeManagerOnChanged(object? sender, EventArgs e)
    {
        ApplyExampleTheme(_themeManager.Theme);
        UpdateThemeState();
    }

    private void ApplyExampleTheme(HiveThemeDefinition theme)
    {
        _navigation.BackColor = theme.VisualStates.NavigationBackground;
        _navigationTitle.ForeColor = theme.VisualStates.NavigationText;
        _navigationDescription.ForeColor = theme.Palette.MutedText;
        _content.BackColor = theme.Palette.Surface;
        _contentLayout.BackColor = theme.Palette.Surface;
        _pageBody.BackColor = theme.Palette.Surface;
        _themePage.BackColor = theme.Palette.Surface;
        _controlsPage.BackColor = theme.Palette.Surface;
        _dialogsPage.BackColor = theme.Palette.Surface;
    }

    private void AddNavigation(
        string text,
        Control page,
        string title,
        string description,
        bool selected = false)
    {
        var button = new HiveButton
        {
            Text = text,
            Style = selected
                ? HiveButtonStyle.NavigationSelected
                : HiveButtonStyle.Navigation,
            Width = 158,
            Height = 38,
            Margin = new Padding(0, 0, 0, 6)
        };

        if (selected)
            _selectedNavigationButton = button;

        button.Click += (_, _) =>
        {
            if (!ReferenceEquals(_selectedNavigationButton, button))
            {
                if (_selectedNavigationButton is not null)
                    _selectedNavigationButton.Style = HiveButtonStyle.Navigation;

                _selectedNavigationButton = button;
                button.Style = HiveButtonStyle.NavigationSelected;
            }

            ShowPage(page, title, description);
        };

        _navigation.Controls.Add(button);
    }

    private static FlowLayoutPanel CreatePage() =>
        new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = false,
            AutoScroll = true,
            Margin = Padding.Empty,
            Padding = new Padding(0, 2, 12, 2),
            Visible = false
        };

    private void BuildControlsPage()
    {
        var input = new TextBox
        {
            Width = 320,
            Height = 28,
            Text = "Native WinForms TextBox",
            Margin = new Padding(0, 0, 0, 8)
        };

        var checkBox = new CheckBox
        {
            AutoSize = true,
            Text = "Native WinForms CheckBox",
            Margin = new Padding(0, 0, 0, 8)
        };

        var disabled = new CheckBox
        {
            AutoSize = true,
            Enabled = false,
            Text = "Disabled control state",
            Margin = new Padding(0, 0, 0, 8)
        };

        var primary = new HiveButton
        {
            Text = "HiveButton — Primary",
            Style = HiveButtonStyle.Primary,
            Width = 190,
            Height = 38,
            Margin = new Padding(0, 8, 0, 0)
        };

        var secondary = new HiveButton
        {
            Text = "HiveButton — Secondary",
            Style = HiveButtonStyle.Secondary,
            Width = 190,
            Height = 38,
            Margin = new Padding(0, 6, 0, 0)
        };

        _controlsPage.Controls.Add(input);
        _controlsPage.Controls.Add(checkBox);
        _controlsPage.Controls.Add(disabled);
        _controlsPage.Controls.Add(primary);
        _controlsPage.Controls.Add(secondary);
    }

    private void BuildListCompositionExample()
    {
        BuildCrudCompositionExample();
    }

    private void BuildDialogsPage()
    {
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
            Height = 38,
            Margin = new Padding(0, 10, 0, 0)
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

        _dialogsPage.Controls.Add(details);
    }

    private HiveButton CreateThemeButton(
        string text,
        HiveThemeMode mode)
    {
        var button = new HiveButton
        {
            Text = text,
            Style = HiveButtonStyle.Secondary,
            Width = 104,
            Height = 36,
            Margin = new Padding(0, 0, 8, 8)
        };

        button.Click += (_, _) =>
        {
            _themeManager.SetMode(mode);
            UpdateThemeState();
        };

        return button;
    }

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
            Height = 38,
            Margin = new Padding(0, 0, 0, 6)
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

        _dialogsPage.Controls.Add(button);
    }

    private void ShowPage(
        Control page,
        string title,
        string description)
    {
        _pageTitle.Text = title;
        _pageDescription.Text = description;

        _pageBody.SuspendLayout();
        try
        {
            _pageBody.Controls.Clear();

            page.Dock = DockStyle.Fill;
            page.Anchor = AnchorStyles.Top |
                          AnchorStyles.Bottom |
                          AnchorStyles.Left |
                          AnchorStyles.Right;
            page.Visible = true;

            _pageBody.Controls.Add(page);
            page.BringToFront();
        }
        finally
        {
            _pageBody.ResumeLayout(true);
        }

        _themeManager.Apply(page);
    }

    private void UpdateThemeState()
    {
        _themeState.Text =
            $"Selected: {_themeManager.Mode}\r\nEffective: {_themeManager.Theme.Mode}";
    }

    private static Label CreateBodyLabel(string? text = null) =>
        new()
        {
            AutoSize = true,
            Text = text ?? string.Empty,
            MaximumSize = new Size(720, 120),
            Margin = new Padding(0, 0, 0, 12)
        };

    private IHiveThemeManager ThemeManager => _themeManager;

    private Panel BodyPanel => _bodyPanel;
}
