using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;
using Hive.Host.WinForms.UI.Controls;

namespace Hive.Host.WinForms.UI.Theme;

public sealed class HiveThemeManager : IHiveThemeManager
{
    private const string WindowsThemeRegistryPath =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private static readonly HivePalette LightPalette = new(
        ColorTranslator.FromHtml("#F4F6F8"),
        ColorTranslator.FromHtml("#FFFFFF"),
        ColorTranslator.FromHtml("#FBFCFE"),
        ColorTranslator.FromHtml("#1D2939"),
        ColorTranslator.FromHtml("#667085"),
        ColorTranslator.FromHtml("#D8DEE8"),
        ColorTranslator.FromHtml("#2563EB"),
        ColorTranslator.FromHtml("#1D4ED8"),
        Color.White,
        ColorTranslator.FromHtml("#FFFFFF"),
        ColorTranslator.FromHtml("#F1F3F5"),
        ColorTranslator.FromHtml("#667085"),
        ColorTranslator.FromHtml("#DCE9FF"));

    private static readonly HivePalette DarkPalette = new(
        ColorTranslator.FromHtml("#0F141A"),
        ColorTranslator.FromHtml("#171D24"),
        ColorTranslator.FromHtml("#1E2630"),
        ColorTranslator.FromHtml("#F2F4F7"),
        ColorTranslator.FromHtml("#98A2B3"),
        ColorTranslator.FromHtml("#2B3642"),
        ColorTranslator.FromHtml("#2E6EC7"),
        ColorTranslator.FromHtml("#3876C6"),
        ColorTranslator.FromHtml("#FFFFFF"),
        ColorTranslator.FromHtml("#12181F"),
        ColorTranslator.FromHtml("#222831"),
        ColorTranslator.FromHtml("#A5AFBA"),
        ColorTranslator.FromHtml("#24466F"));

    private HiveThemeDefinition _theme;

    public HiveThemeManager(HiveThemeMode mode = HiveThemeMode.System)
    {
        Mode = mode;
        _theme = CreateTheme(ResolveEffectiveMode(mode));
    }

    public HiveThemeMode Mode { get; private set; }

    public HiveThemeDefinition Theme => _theme;

    public event EventHandler? ThemeChanged;

    public void SetMode(HiveThemeMode mode)
    {
        if (Mode == mode)
            return;

        Mode = mode;
        _theme = CreateTheme(ResolveEffectiveMode(mode));
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Apply(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);

        // Theme application must be paint-only. Changing visual state must not
        // trigger a layout pass, move AutoScroll positions, or mutate control
        // geometry. All sizing/layout belongs to control initialization/resizing.
        ApplyControl(root, Theme);
        root.Invalidate(true);
    }

    private static void ApplyControl(
        Control control,
        HiveThemeDefinition theme)
    {
        switch (control)
        {
            case Form:
                SetBackColor(control, theme.Palette.WindowBackground);
                SetForeColor(control, theme.Palette.Text);
                break;

            case TabPage:
            case Panel:
            case UserControl:
            case GroupBox:
                SetBackColor(control, theme.Palette.Surface);
                SetForeColor(control, theme.Palette.Text);
                break;

            case Button:
                SetBackColor(
                    control,
                    control.Enabled
                        ? theme.Palette.Accent
                        : theme.Palette.DisabledBackground);
                SetForeColor(
                    control,
                    control.Enabled
                        ? theme.Palette.AccentForeground
                        : theme.Palette.DisabledText);
                break;

            case TextBoxBase textBox:
                SetBackColor(
                    textBox,
                    textBox.Enabled
                        ? theme.Palette.InputBackground
                        : theme.Palette.DisabledBackground);
                SetForeColor(
                    textBox,
                    textBox.Enabled
                        ? theme.Palette.Text
                        : theme.Palette.DisabledText);
                break;

            case ComboBox comboBox:
                SetBackColor(
                    comboBox,
                    comboBox.Enabled
                        ? theme.Palette.InputBackground
                        : theme.Palette.DisabledBackground);
                SetForeColor(
                    comboBox,
                    comboBox.Enabled
                        ? theme.Palette.Text
                        : theme.Palette.DisabledText);
                break;

            case ListBox listBox:
                SetBackColor(
                    listBox,
                    listBox.Enabled
                        ? theme.Palette.InputBackground
                        : theme.Palette.DisabledBackground);
                SetForeColor(
                    listBox,
                    listBox.Enabled
                        ? theme.Palette.Text
                        : theme.Palette.DisabledText);
                break;

            case NumericUpDown numericUpDown:
                SetBackColor(
                    numericUpDown,
                    numericUpDown.Enabled
                        ? theme.Palette.InputBackground
                        : theme.Palette.DisabledBackground);
                SetForeColor(
                    numericUpDown,
                    numericUpDown.Enabled
                        ? theme.Palette.Text
                        : theme.Palette.DisabledText);
                break;

            case DomainUpDown domainUpDown:
                SetBackColor(
                    domainUpDown,
                    domainUpDown.Enabled
                        ? theme.Palette.InputBackground
                        : theme.Palette.DisabledBackground);
                SetForeColor(
                    domainUpDown,
                    domainUpDown.Enabled
                        ? theme.Palette.Text
                        : theme.Palette.DisabledText);
                break;

            case DateTimePicker dateTimePicker:
                dateTimePicker.CalendarMonthBackground = theme.Palette.InputBackground;
                dateTimePicker.CalendarForeColor = theme.Palette.Text;
                SetBackColor(
                    dateTimePicker,
                    dateTimePicker.Enabled
                        ? theme.Palette.InputBackground
                        : theme.Palette.DisabledBackground);
                SetForeColor(
                    dateTimePicker,
                    dateTimePicker.Enabled
                        ? theme.Palette.Text
                        : theme.Palette.DisabledText);
                break;

            case ListView:
            case TreeView:
                SetBackColor(control, theme.Palette.Surface);
                SetForeColor(
                    control,
                    control.Enabled
                        ? theme.Palette.Text
                        : theme.Palette.DisabledText);
                break;

            case CheckBox:
            case RadioButton:
                SetBackColor(control, Color.Transparent);
                SetForeColor(
                    control,
                    control.Enabled
                        ? theme.Palette.Text
                        : theme.Palette.DisabledText);
                break;

            case LinkLabel:
            case Label:
                SetForeColor(
                    control,
                    control.Enabled
                        ? theme.Palette.Text
                        : theme.Palette.DisabledText);
                SetBackColor(control, Color.Transparent);
                break;

            case DataGridView grid:
                ApplyDataGridViewTheme(grid, theme);
                break;
        }

        foreach (Control child in control.Controls)
            ApplyControl(child, theme);

        switch (control)
        {
            case HiveNavigationTree navigationTree:
                navigationTree.ApplyTheme(theme);
                break;

            case HiveListView listView:
                listView.ApplyTheme(theme);
                break;

            case HiveBorderPanel borderPanel:
                borderPanel.ApplyTheme(theme);
                break;

            case HiveButton hiveButton:
                hiveButton.ApplyTheme(theme);
                break;

            case HiveEditorLayout editorLayout:
                editorLayout.ApplyTheme(theme);
                break;

            case HiveListPageLayout listPageLayout:
                listPageLayout.ApplyTheme(theme);
                break;

            case HivePaginationBar paginationBar:
                paginationBar.ApplyTheme(theme);
                break;

            case HiveExampleTestSurface exampleTestSurface:
                exampleTestSurface.ApplyTheme(theme);
                break;

            case HiveExampleOutputView exampleOutputView:
                exampleOutputView.ApplyTheme(theme);
                break;
        }
    }

    private static void ApplyDataGridViewTheme(
        DataGridView grid,
        HiveThemeDefinition theme)
    {
        grid.BackgroundColor = theme.Palette.WindowBackground;
        grid.GridColor = theme.Palette.Border;
        grid.EnableHeadersVisualStyles = false;

        grid.DefaultCellStyle.BackColor = theme.Palette.InputBackground;
        grid.DefaultCellStyle.ForeColor = theme.Palette.Text;
        grid.DefaultCellStyle.SelectionBackColor = theme.Palette.Selection;
        grid.DefaultCellStyle.SelectionForeColor = theme.Palette.Text;

        grid.AlternatingRowsDefaultCellStyle.BackColor =
            theme.Palette.Surface;
        grid.AlternatingRowsDefaultCellStyle.ForeColor =
            theme.Palette.Text;
        grid.AlternatingRowsDefaultCellStyle.SelectionBackColor =
            theme.Palette.Selection;
        grid.AlternatingRowsDefaultCellStyle.SelectionForeColor =
            theme.Palette.Text;

        grid.ColumnHeadersDefaultCellStyle.BackColor = theme.Palette.Surface;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = theme.Palette.Text;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = theme.Palette.Surface;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = theme.Palette.Text;

        grid.RowHeadersDefaultCellStyle.BackColor = theme.Palette.Surface;
        grid.RowHeadersDefaultCellStyle.ForeColor = theme.Palette.Text;
        grid.RowHeadersDefaultCellStyle.SelectionBackColor = theme.Palette.Selection;
        grid.RowHeadersDefaultCellStyle.SelectionForeColor = theme.Palette.Text;

        if (!grid.Enabled)
        {
            grid.DefaultCellStyle.BackColor = theme.Palette.DisabledBackground;
            grid.DefaultCellStyle.ForeColor = theme.Palette.DisabledText;
            grid.DefaultCellStyle.SelectionBackColor = theme.Palette.DisabledBackground;
            grid.DefaultCellStyle.SelectionForeColor = theme.Palette.DisabledText;
            grid.AlternatingRowsDefaultCellStyle.BackColor =
                theme.Palette.DisabledBackground;
            grid.AlternatingRowsDefaultCellStyle.ForeColor =
                theme.Palette.DisabledText;
            grid.AlternatingRowsDefaultCellStyle.SelectionBackColor =
                theme.Palette.DisabledBackground;
            grid.AlternatingRowsDefaultCellStyle.SelectionForeColor =
                theme.Palette.DisabledText;

            grid.ColumnHeadersDefaultCellStyle.BackColor =
                theme.Palette.DisabledBackground;
            grid.ColumnHeadersDefaultCellStyle.ForeColor =
                theme.Palette.DisabledText;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor =
                theme.Palette.DisabledBackground;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor =
                theme.Palette.DisabledText;

            grid.RowHeadersDefaultCellStyle.BackColor =
                theme.Palette.DisabledBackground;
            grid.RowHeadersDefaultCellStyle.ForeColor =
                theme.Palette.DisabledText;
            grid.RowHeadersDefaultCellStyle.SelectionBackColor =
                theme.Palette.DisabledBackground;
            grid.RowHeadersDefaultCellStyle.SelectionForeColor =
                theme.Palette.DisabledText;
        }
    }

    private static void SetBackColor(Control control, Color color)
    {
        if (control.BackColor != color)
            control.BackColor = color;
    }

    private static void SetForeColor(Control control, Color color)
    {
        if (control.ForeColor != color)
            control.ForeColor = color;
    }

    private static HiveThemeDefinition CreateTheme(
        HiveThemeMode effectiveMode)
    {
        var palette = effectiveMode == HiveThemeMode.Dark
            ? DarkPalette
            : LightPalette;

        var dark = effectiveMode == HiveThemeMode.Dark;

        return new HiveThemeDefinition(
            effectiveMode,
            palette,
            new HiveTypography("Segoe UI", 9.25f, 15.5f)
            {
                SmallSize = 8.5f,
                SectionSize = 9.25f,
                TitleSize = 16f,
                MonospaceSize = 9f
            },
            new HiveSpacing(4, 8, 12, 16, 24),
            new HiveVisualStates(
                dark
                    ? ColorTranslator.FromHtml("#202A35")
                    : ColorTranslator.FromHtml("#F1F5FA"),
                dark
                    ? ColorTranslator.FromHtml("#293541")
                    : ColorTranslator.FromHtml("#E5EBF2"),
                palette.Accent,
                dark
                    ? ColorTranslator.FromHtml("#3A4652")
                    : ColorTranslator.FromHtml("#C9D0DA"),
                palette.DisabledText)
            {
                NavigationBackground = dark
                    ? ColorTranslator.FromHtml("#141A21")
                    : ColorTranslator.FromHtml("#F8FAFC"),
                NavigationHover = dark
                    ? ColorTranslator.FromHtml("#1D2731")
                    : ColorTranslator.FromHtml("#F0F5FB"),
                NavigationPressed = dark
                    ? ColorTranslator.FromHtml("#25323E")
                    : ColorTranslator.FromHtml("#E7EFF9"),
                NavigationSelected = dark
                    ? ColorTranslator.FromHtml("#203A58")
                    : ColorTranslator.FromHtml("#E1ECFF"),
                NavigationSelectedText = dark
                    ? ColorTranslator.FromHtml("#D8E8FF")
                    : ColorTranslator.FromHtml("#194185"),
                NavigationText = dark
                    ? ColorTranslator.FromHtml("#D0D5DD")
                    : ColorTranslator.FromHtml("#344054"),
                NavigationBorder = dark
                    ? ColorTranslator.FromHtml("#2A3541")
                    : ColorTranslator.FromHtml("#D8DEE8"),
                Information = dark
                    ? ColorTranslator.FromHtml("#76B0FF")
                    : ColorTranslator.FromHtml("#2563EB"),
                Success = dark
                    ? ColorTranslator.FromHtml("#6CCB91")
                    : ColorTranslator.FromHtml("#168A4A"),
                Warning = dark
                    ? ColorTranslator.FromHtml("#E9B949")
                    : ColorTranslator.FromHtml("#B77908"),
                Error = dark
                    ? ColorTranslator.FromHtml("#C94C4C")
                    : ColorTranslator.FromHtml("#C73636"),
                Question = dark
                    ? ColorTranslator.FromHtml("#A7B1BF")
                    : ColorTranslator.FromHtml("#667085")
            });
    }

    private static HiveThemeMode ResolveEffectiveMode(
        HiveThemeMode mode)
    {
        if (mode != HiveThemeMode.System)
            return mode;

        try
        {
            using var key =
                Registry.CurrentUser.OpenSubKey(WindowsThemeRegistryPath);

            return key?.GetValue("AppsUseLightTheme") is int value && value == 0
                ? HiveThemeMode.Dark
                : HiveThemeMode.Light;
        }
        catch (System.Security.SecurityException)
        {
            return HiveThemeMode.Light;
        }
        catch (UnauthorizedAccessException)
        {
            return HiveThemeMode.Light;
        }
        catch (System.IO.IOException)
        {
            return HiveThemeMode.Light;
        }
    }
}
