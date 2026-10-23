using System.Drawing;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

public sealed class HiveCardPanel : HiveBorderPanel
{
    public HiveCardPanel()
    {
        Padding = new Padding(12);
        Margin = Padding.Empty;
    }

    public new void ApplyTheme(HiveThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        CornerRadius = Math.Max(0, theme.Radii.Medium);
        BorderColor = theme.Palette.CardBorder;
        BackColor = theme.Palette.Surface;
        base.ApplyTheme(theme);
    }
}