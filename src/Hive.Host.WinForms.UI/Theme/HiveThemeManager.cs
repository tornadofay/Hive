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
        ColorTranslator.FromHtml("#FFFFFF"),
        ColorTranslator.FromHtml("#1D2939"),
        ColorTranslator.FromHtml("#667085"),
        ColorTranslator.FromHtml("#D8DEE8"),
        ColorTranslator.FromHtml("#2563EB"),
        ColorTranslator.FromHtml("#1D4ED8"),
        Color.White,
        ColorTranslator.FromHtml("#FFFFFF"),
        ColorTranslator.FromHtml("#F1F3F5"),
        ColorTranslator.FromHtml("#98A2B3"),
        ColorTranslator.FromHtml("#DCE9FF"));

    private static readonly HivePalette DarkPalette = new(
        ColorTranslator.FromHtml("#0F141A"),
        ColorTranslator.FromHtml("#171D24"),
        ColorTranslator.FromHtml("#1E2630"),
        ColorTranslator.FromHtml("#F2F4F7"),
        ColorTranslator.FromHtml("#98A2B3"),
        ColorTranslator.FromHtml("#2B3642"),
        ColorTranslator.FromHtml("#5B9DFF"),
        ColorTranslator.FromHtml("#76B0FF"),
        ColorTranslator.FromHtml("#FFFFFF"),
        ColorTranslator.FromHtml("#12181F"),
        ColorTranslator.FromHtml("#222831"),
        ColorTranslator.FromHtml("#6F7A86"),
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

        root.SuspendLayout();
        try
        {
            ApplyControl(root, Theme);
        }
        finally
        {
            root.ResumeLayout(false);
        }

        root.Invalidate();
    }

    private static void ApplyControl(
        Control control,
        HiveThemeDefinition theme)
    {
        switch (control)
        {
            case HiveNavigationTree navigationTree:
                navigationTree.ApplyTheme(theme);
                return;

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

            case TextBoxBase:
            case ComboBox:
            case ListBox:
            case NumericUpDown:
            case DomainUpDown:
            case DateTimePicker:
                SetBackColor(
                    control,
                    control.Enabled
                        ? theme.Palette.InputBackground
                        : theme.Palette.DisabledBackground);
                SetForeColor(
                    control,
                    control.Enabled
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
            case LinkLabel:
            case Label:
                SetForeColor(
                    control,
                    control.Enabled
                        ? theme.Palette.Text
                        : theme.Palette.DisabledText);
                if (control is Label)
                    SetBackColor(control, Color.Transparent);
                break;

            case DataGridView grid:
                ApplyDataGridViewTheme(grid, theme);
                break;
        }

        foreach (Control child in control.Controls)
            ApplyControl(child, theme);
    }

    private static void ApplyDataGridViewTheme(
        DataGridView grid,
        HiveThemeDefinition theme)
    {
        grid.BackgroundColor = theme.Palette.WindowBackground;
        grid.GridColor = theme.Palette.Border;
        grid.DefaultCellStyle.BackColor = theme.Palette.InputBackground;
        grid.DefaultCellStyle.ForeColor = theme.Palette.Text;
        grid.EnableHeadersVisualStyles = false;
        grid.DefaultCellStyle.SelectionBackColor = theme.Palette.Selection;
        grid.DefaultCellStyle.SelectionForeColor = theme.Palette.Text;
        grid.ColumnHeadersDefaultCellStyle.BackColor = theme.Palette.Surface;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = theme.Palette.Text;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = theme.Palette.Selection;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = theme.Palette.Text;
        grid.RowHeadersDefaultCellStyle.BackColor = theme.Palette.Surface;
        grid.RowHeadersDefaultCellStyle.ForeColor = theme.Palette.Text;
        grid.RowHeadersDefaultCellStyle.SelectionBackColor = theme.Palette.Selection;
        grid.RowHeadersDefaultCellStyle.SelectionForeColor = theme.Palette.Text;
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
            new HiveTypography("Segoe UI", 9.25f, 15f),
            new HiveSpacing(4, 8, 12, 16, 24),
            new HiveVisualStates(
                dark
                    ? ColorTranslator.FromHtml("#202A35")
                    : ColorTranslator.FromHtml("#F1F5FA"),
                dark
                    ? ColorTranslator.FromHtml("#293541")
                    : ColorTranslator.FromHtml("#E5EBF2"),
                palette.Accent,
                palette.Border,
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
                    ? ColorTranslator.FromHtml("#EE7777")
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
    }
}
