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
    private readonly Panel _pageBody;
    private readonly FlowLayoutPanel _themePage;
    private readonly FlowLayoutPanel _controlsPage;
    private readonly FlowLayoutPanel _dialogsPage;
    private readonly Label _pageTitle;
    private readonly Label _pageDescription;
    private readonly Label _themeState;
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
            Width = Scale(190),
            Padding = Scale(new Padding(12, 18, 12, 12)),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Margin = Padding.Empty
        };

        _content = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = Scale(new Padding(28, 24, 28, 24))
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
            MaximumSize = Scale(new Size(800, 80)),
            Margin = Scale(new Padding(0, 8, 0, 20))
        };

        _pageBody = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
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
        _themeState.Margin = Scale(new Padding(0, 18, 0, 0));
        _themePage.Controls.Add(_themeState);

        _controlsPage.Controls.Add(CreateBodyLabel(
            "Hive-prefixed controls exist only where Hive adds a consumer-facing contract or styling beyond ordinary WinForms."));
        BuildControlsPage();
        BuildListCompositionExample();

        _dialogsPage.Controls.Add(CreateBodyLabel(
            "HiveMessageBox provides semantic Information, Success, Warning, Error, and Question dialogs with optional technical details."));
        BuildDialogsPage();

        _contentLayout.Controls.Add(_pageTitle, 0, 0);
        _contentLayout.Controls.Add(_pageDescription, 0, 1);
        _contentLayout.Controls.Add(_pageBody, 0, 2);

        _content.Controls.Add(_contentLayout);
        BodyPanel.Controls.Add(_content);
        BodyPanel.Controls.Add(_navigation);

        AddNavigation("Theme", () => ShowPage(
            _themePage,
            "Theme",
            "Light, Dark, and System modes use Hive-owned semantic tokens and can be switched at runtime."));
        AddNavigation("Controls", () => ShowPage(
            _controlsPage,
            "Controls",
            "Hive-prefixed controls exist only where Hive adds a consumer-facing contract or styling beyond ordinary WinForms."));
        AddNavigation("Dialogs", () => ShowPage(
            _dialogsPage,
            "Dialogs",
            "HiveMessageBox provides semantic Information, Success, Warning, Error, and Question dialogs with optional technical details."));

        _themeManager.Apply(BodyPanel);
        ApplyExampleTheme(Theme);
        ShowPage(
            _themePage,
            "Theme",
            "Light, Dark, and System modes use Hive-owned semantic tokens and can be switched at runtime.");
        UpdateThemeState();
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

    private void ThemeManagerOnChanged(object? sender, EventArgs e)
    {
        ApplyExampleTheme(_themeManager.Theme);
        UpdateThemeState();
    }

    private void ApplyExampleTheme(HiveThemeDefinition theme)
    {
        _navigation.BackColor = theme.VisualStates.NavigationBackground;
        _content.BackColor = theme.Palette.Surface;
        _contentLayout.BackColor = theme.Palette.Surface;
        _pageBody.BackColor = theme.Palette.Surface;
        _themePage.BackColor = theme.Palette.Surface;
        _controlsPage.BackColor = theme.Palette.Surface;
        _dialogsPage.BackColor = theme.Palette.Surface;
    }

    private void AddNavigation(string text, Action action)
    {
        var button = new HiveButton
        {
            Text = text,
            Style = HiveButtonStyle.Navigation,
            Width = Scale(166),
            Height = Scale(42),
            Margin = Scale(new Padding(0, 0, 0, 8))
        };

        button.Click += (_, _) => action();
        _navigation.Controls.Add(button);
    }

    private FlowLayoutPanel CreatePage() =>
        new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = false,
            AutoScroll = true,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Visible = false
        };

    private void BuildControlsPage()
    {
        var input = new TextBox
        {
            Width = Scale(320),
            Height = Scale(28),
            Text = "Native WinForms TextBox",
            Margin = Scale(new Padding(0, 0, 0, 8))
        };

        var checkBox = new CheckBox
        {
            AutoSize = true,
            Text = "Native WinForms CheckBox",
            Margin = Scale(new Padding(0, 0, 0, 8))
        };

        var disabled = new CheckBox
        {
            AutoSize = true,
            Enabled = false,
            Text = "Disabled control state",
            Margin = Scale(new Padding(0, 0, 0, 2))
        };

        var primary = new HiveButton
        {
            Text = "HiveButton — Primary",
            Style = HiveButtonStyle.Primary,
            Width = Scale(190),
            Height = Scale(40),
            Margin = Scale(new Padding(0, 10, 0, 0))
        };

        var secondary = new HiveButton
        {
            Text = "HiveButton — Secondary",
            Style = HiveButtonStyle.Secondary,
            Width = Scale(190),
            Height = Scale(40),
            Margin = Scale(new Padding(0, 8, 0, 0))
        };

        _controlsPage.Controls.Add(input);
        _controlsPage.Controls.Add(checkBox);
        _controlsPage.Controls.Add(disabled);
        _controlsPage.Controls.Add(primary);
        _controlsPage.Controls.Add(secondary);
    }

    private void BuildListCompositionExample()
    {
        var listPage = new HiveListPageLayout
        {
            Width = Scale(760),
            Height = Scale(280),
            Margin = Scale(new Padding(0, 18, 0, 12))
        };

        listPage.HeaderPanel.Padding = Scale(new Padding(12, 8, 12, 4));
        listPage.HeaderPanel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Reusable list composition",
            Font = CreateOwnedBoldFont(11f),
            ForeColor = Theme.Palette.Text
        });

        listPage.ActionBarPanel.Padding = Scale(new Padding(12, 6, 12, 6));
        listPage.ActionBarPanel.Controls.Add(new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Text = "Domain pages supply their own filters, CRUD commands, columns, and editors.",
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Palette.MutedText
        });

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, Scale(44)));

        var list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            BorderStyle = BorderStyle.FixedSingle
        };
        list.Columns.Add("Name", Scale(220));
        list.Columns.Add("Type", Scale(160));
        list.Columns.Add("Status", Scale(140));
        list.Items.Add(new ListViewItem(new[] { "Example provider", "Provider", "Enabled" }));
        list.Items.Add(new ListViewItem(new[] { "Example agent", "Agent", "Enabled" }));
        list.Items.Add(new ListViewItem(new[] { "Example resource", "Resource", "Ready" }));

        var pager = new HivePaginationBar
        {
            CanGoNext = true,
            CanGoPrevious = false
        };
        pager.NextRequested += (_, _) => pager.CanGoPrevious = true;
        pager.PreviousRequested += (_, _) => pager.CanGoPrevious = pager.PageNumber > 1;

        content.Controls.Add(list, 0, 0);
        content.Controls.Add(pager, 0, 1);
        listPage.ContentPanel.Controls.Add(content);

        _controlsPage.Controls.Add(listPage);
        ThemeManager.Apply(listPage);
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
            Width = Scale(220),
            Height = Scale(40),
            Margin = Scale(new Padding(0, 12, 0, 0))
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

    private HiveButton CreateThemeButton(string text, HiveThemeMode mode)
    {
        var button = new HiveButton
        {
            Text = text,
            Style = HiveButtonStyle.Secondary,
            Width = Scale(110),
            Height = Scale(38),
            Margin = Scale(new Padding(0, 0, 0, 8))
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
            Width = Scale(190),
            Height = Scale(40),
            Margin = Scale(new Padding(0, 0, 0, 8))
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

        if (!ReferenceEquals(page.Parent, _pageBody))
        {
            _pageBody.SuspendLayout();
            try
            {
                _pageBody.Controls.Clear();
                page.Dock = DockStyle.Fill;
                page.Visible = true;
                _pageBody.Controls.Add(page);
            }
            finally
            {
                _pageBody.ResumeLayout(true);
            }
        }

        _themeManager.Apply(page);
    }

    private void UpdateThemeState() =>
        _themeState.Text = $"Selected: {_themeManager.Mode}\r\nEffective: {_themeManager.Theme.Mode}";

    private Label CreateBodyLabel(string? text = null) =>
        new()
        {
            AutoSize = true,
            Text = text ?? string.Empty,
            MaximumSize = Scale(new Size(700, 120)),
            Margin = Scale(new Padding(0, 0, 0, 12))
        };
}
