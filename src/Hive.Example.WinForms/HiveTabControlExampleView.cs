using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed class HiveTabControlExampleView : UserControl
{
    private readonly IHiveThemeManager _themeManager;
    private readonly HiveTabControl _tabs;
    private readonly Label _stateLabel;
    private readonly Label _themeLabel;
    private readonly Font _titleFont;
    private readonly Font _sectionFont;

    public HiveTabControlExampleView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _themeManager = services.GetThemeManager();

        var typography = _themeManager.Theme.Typography;
        _titleFont = new Font(
            typography.FontFamily,
            typography.TitleSize,
            FontStyle.Bold);
        _sectionFont = new Font(
            typography.FontFamily,
            typography.SectionSize,
            FontStyle.Bold);

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = new Padding(16);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 0,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            AutoScroll = false
        };
        root.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100f));

        var title = new Label
        {
            AutoSize = true,
            Font = _titleFont,
            Text = "HiveTabControl",
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        var description = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(980, 72),
            Text =
                "This example uses the Hive-owned tab header renderer with conventional TabPage hosting. It demonstrates selection, disabled tabs, keyboard navigation, page-state preservation, and horizontal overflow using the Hive scrollbar.",
            Margin = new Padding(0, 8, 0, 14)
        };

        var section = new Label
        {
            AutoSize = true,
            Font = _sectionFont,
            Text = "Tab navigation and overflow",
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        _tabs = new HiveTabControl
        {
            Name = "hiveTabControlExample",
            Dock = DockStyle.Fill,
            MinimumSize = new Size(0, 260),
            AccessibleName = "Hive tab control example"
        };

        AddPage(
            "Overview",
            "Overview content remains mounted while switching tabs. Use the counter button below to verify page-state preservation.");
        AddPage(
            "Configuration and Provider Defaults",
            "A deliberately long tab label demonstrates deterministic header sizing and horizontal overflow.");
        AddPage(
            "Execution Target Metadata",
            "Programmatic selection and keyboard navigation use the same selected page model.");
        AddPage(
            "Persistence and Runtime",
            "Switch away and return to verify the same TabPage instance remains active.");
        AddPage(
            "Input Preparation and Routing",
            "The header renderer remains independent of the page content.");
        AddPage(
            "Capability Discovery",
            "Selected, hover, focus, and overflow presentation are owned by HiveTabControl.");
        AddPage(
            "Management Governance",
            "This tab represents ordinary WinForms content hosted by Hive.");
        AddPage(
            "Workspace Interaction",
            "The page content is stable across tab changes and theme transitions.");
        var disabled = AddPage(
            "Disabled Example Tab",
            "This tab is intentionally disabled. It should not be selected by mouse or keyboard navigation.");
        disabled.Enabled = false;
        AddPage(
            "Human Review and Approval",
            "Long labels remain readable because the header surface scrolls rather than compressing indefinitely.");
        AddPage(
            "Diagnostics and Resource Inventory",
            "Use the horizontal Hive scrollbar when headers exceed the available width.");
        AddPage(
            "Final Long Configuration Surface",
            "The active tab remains visible when selection changes near the end of an overflowing header strip.");

        var instructions = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(980, 66),
            Margin = new Padding(0, 10, 0, 0),
            Text =
                "Mouse: click a tab. Keyboard: focus the header, then Left/Right/Home/End; Ctrl+Tab and Ctrl+Shift+Tab also work. Use the horizontal Hive scrollbar when the headers overflow. Change the Example Host theme to Light, Dark, and System."
        };

        _stateLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(980, 80),
            Margin = new Padding(0, 8, 0, 0),
            Padding = Padding.Empty
        };

        _themeLabel = new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0),
            Padding = Padding.Empty,
            Text = GetThemeText()
        };

        _tabs.SelectedIndexChanged += TabsOnSelectionChanged;

        root.Controls.Add(title, 0, root.RowCount++);
        root.Controls.Add(description, 0, root.RowCount++);
        root.Controls.Add(section, 0, root.RowCount++);
        root.Controls.Add(_tabs, 0, root.RowCount++);
        root.Controls.Add(instructions, 0, root.RowCount++);
        root.Controls.Add(_stateLabel, 0, root.RowCount++);
        root.Controls.Add(_themeLabel, 0, root.RowCount++);

        Controls.Add(root);

        _themeManager.ThemeChanged += ThemeManagerOnChanged;
        UpdateStateText();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _themeManager.ThemeChanged -= ThemeManagerOnChanged;
            _tabs.SelectedIndexChanged -= TabsOnSelectionChanged;
            _titleFont.Dispose();
            _sectionFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private TabPage AddPage(string title, string description)
    {
        var page = new TabPage(title)
        {
            Padding = new Padding(16)
        };

        var label = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(720, 70),
            Font = _sectionFont,
            Text = description,
            Dock = DockStyle.Top,
            Margin = Padding.Empty
        };

        page.Controls.Add(label);
        _tabs.TabPages.Add(page);
        return page;
    }

    private void TabsOnSelectionChanged(object? sender, EventArgs e) =>
        UpdateStateText();

    private void ThemeManagerOnChanged(object? sender, EventArgs e) =>
        _themeLabel.Text = GetThemeText();

    private void UpdateStateText()
    {
        var selected = _tabs.SelectedTab;

        _stateLabel.Text =
            $"SelectedIndex: {_tabs.SelectedIndex}    " +
            $"SelectedTab: {selected?.Text ?? "<none>"}    " +
            $"TabCount: {_tabs.TabPages.Count}";
    }

    private string GetThemeText() =>
        $"Active theme mode: {_themeManager.Mode}. Use the Example Host theme control to verify Light, Dark, and System rendering.";
}
