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
        ColorTranslator.FromHtml("#F6F7F9"),
        ColorTranslator.FromHtml("#FFFFFF"),
        ColorTranslator.FromHtml("#FFFFFF"),
        ColorTranslator.FromHtml("#1F2937"),
        ColorTranslator.FromHtml("#6B7280"),
        ColorTranslator.FromHtml("#D1D5DB"),
        ColorTranslator.FromHtml("#2563EB"),
        ColorTranslator.FromHtml("#1D4ED8"),
        Color.White,
        Color.White,
        ColorTranslator.FromHtml("#F3F4F6"),
        ColorTranslator.FromHtml("#9CA3AF"),
        ColorTranslator.FromHtml("#DBEAFE"));

    private static readonly HivePalette DarkPalette = new(
        ColorTranslator.FromHtml("#111827"),
        ColorTranslator.FromHtml("#1F2937"),
        ColorTranslator.FromHtml("#243244"),
        ColorTranslator.FromHtml("#F3F4F6"),
        ColorTranslator.FromHtml("#9CA3AF"),
        ColorTranslator.FromHtml("#374151"),
        ColorTranslator.FromHtml("#3B82F6"),
        ColorTranslator.FromHtml("#60A5FA"),
        Color.White,
        ColorTranslator.FromHtml("#1F2937"),
        ColorTranslator.FromHtml("#374151"),
        ColorTranslator.FromHtml("#6B7280"),
        ColorTranslator.FromHtml("#1E3A5F"));

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
        ApplyControl(root, Theme);
    }

    private static void ApplyControl(Control control, HiveThemeDefinition theme)
    {
        switch (control)
        {
            case Form:
                control.BackColor = theme.Palette.WindowBackground;
                control.ForeColor = theme.Palette.Text;
                break;

            case HiveButton hiveButton:
                hiveButton.ApplyTheme(theme);
                break;

            case Panel:
            case UserControl:
            case GroupBox:
            case TabPage:
                control.BackColor = theme.Palette.Surface;
                control.ForeColor = theme.Palette.Text;
                break;

            case Button:
                control.BackColor = control.Enabled
                    ? theme.Palette.Accent
                    : theme.Palette.DisabledBackground;
                control.ForeColor = control.Enabled
                    ? theme.Palette.AccentForeground
                    : theme.Palette.DisabledText;
                break;

            case TextBoxBase:
            case ComboBox:
            case ListBox:
                control.BackColor = control.Enabled
                    ? theme.Palette.InputBackground
                    : theme.Palette.DisabledBackground;
                control.ForeColor = control.Enabled
                    ? theme.Palette.Text
                    : theme.Palette.DisabledText;
                break;

            case CheckBox:
            case RadioButton:
            case LinkLabel:
            case Label:
                control.ForeColor = control.Enabled
                    ? theme.Palette.Text
                    : theme.Palette.DisabledText;
                control.BackColor = Color.Transparent;
                break;

            case DataGridView grid:
                grid.BackgroundColor = theme.Palette.WindowBackground;
                grid.GridColor = theme.Palette.Border;
                grid.DefaultCellStyle.BackColor = theme.Palette.InputBackground;
                grid.DefaultCellStyle.ForeColor = theme.Palette.Text;
                grid.DefaultCellStyle.SelectionBackColor = theme.Palette.Selection;
                grid.DefaultCellStyle.SelectionForeColor = theme.Palette.Text;
                grid.ColumnHeadersDefaultCellStyle.BackColor = theme.Palette.Surface;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = theme.Palette.Text;
                grid.RowHeadersDefaultCellStyle.BackColor = theme.Palette.Surface;
                grid.RowHeadersDefaultCellStyle.ForeColor = theme.Palette.Text;
                break;
        }

        foreach (Control child in control.Controls)
            ApplyControl(child, theme);
    }

    private static HiveThemeDefinition CreateTheme(HiveThemeMode effectiveMode)
    {
        var palette = effectiveMode == HiveThemeMode.Dark
            ? DarkPalette
            : LightPalette;

        return new HiveThemeDefinition(
            effectiveMode,
            palette,
            new HiveTypography("Segoe UI", 9f, 13f),
            new HiveSpacing(4, 8, 12, 16, 24),
            new HiveVisualStates(
                palette.ElevatedSurface,
                palette.Border,
                palette.Accent,
                palette.Border,
                palette.DisabledText));
    }

    private static HiveThemeMode ResolveEffectiveMode(HiveThemeMode mode)
    {
        if (mode != HiveThemeMode.System)
            return mode;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(WindowsThemeRegistryPath);
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
