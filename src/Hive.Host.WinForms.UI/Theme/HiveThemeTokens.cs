using System.Drawing;

namespace Hive.Host.WinForms.UI.Theme;

public sealed record HivePalette(
    Color WindowBackground,
    Color Surface,
    Color ElevatedSurface,
    Color Text,
    Color MutedText,
    Color Border,
    Color Accent,
    Color AccentHover,
    Color AccentForeground,
    Color InputBackground,
    Color DisabledBackground,
    Color DisabledText,
    Color Selection);

public sealed record HiveTypography(
    string FontFamily,
    float BodySize,
    float HeadingSize)
{
    public float SmallSize { get; init; } = 8.5f;
    public float SectionSize { get; init; } = 9.25f;
    public float TitleSize { get; init; } = 16f;
    public float MonospaceSize { get; init; } = 9f;
}

public sealed record HiveSpacing(
    int Xs,
    int Sm,
    int Md,
    int Lg,
    int Xl);

public sealed record HiveVisualStates(
    Color HoverBackground,
    Color PressedBackground,
    Color FocusedBorder,
    Color DisabledBorder,
    Color DisabledText)
{
    public Color NavigationBackground { get; init; }
    public Color NavigationHover { get; init; }
    public Color NavigationPressed { get; init; }
    public Color NavigationSelected { get; init; }
    public Color NavigationSelectedText { get; init; }
    public Color NavigationText { get; init; }
    public Color NavigationBorder { get; init; }
    public Color DisabledListBackground { get; init; }
    public Color Information { get; init; }
    public Color Success { get; init; }
    public Color Warning { get; init; }
    public Color Error { get; init; }
    public Color Question { get; init; }
}

public sealed record HiveThemeDefinition(
    HiveThemeMode Mode,
    HivePalette Palette,
    HiveTypography Typography,
    HiveSpacing Spacing,
    HiveVisualStates VisualStates);
