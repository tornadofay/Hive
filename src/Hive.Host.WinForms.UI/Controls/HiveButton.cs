using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Windows.Forms;
using ReaLTaiizor.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

public enum HiveButtonStyle
{
    Primary,
    Secondary,
    Navigation,
    NavigationSelected,
    Danger
}

public sealed class HiveButton : UserControl
{
    private readonly MaterialButton _renderer;
    private HiveButtonStyle _style = HiveButtonStyle.Primary;

    public HiveButton()
    {
        _renderer = new MaterialButton
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            HighEmphasis = true,
            Text = "Button",
            UseAccentColor = true,
            Depth = 1
        };

        _renderer.Click += (_, e) => OnClick(e);
        Controls.Add(_renderer);

        MinimumSize = new Size(88, 36);
        Size = new Size(120, 38);
        Margin = new Padding(0);
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.PushButton;
    }

    [Browsable(true)]
    [DefaultValue("Button")]
    [AllowNull]
    public override string Text
    {
        get => _renderer.Text;
        set => _renderer.Text = value ?? string.Empty;
    }

    [DefaultValue(HiveButtonStyle.Primary)]
    public HiveButtonStyle Style
    {
        get => _style;
        set
        {
            if (_style == value)
                return;

            _style = value;
            ApplyCurrentTheme();
        }
    }

    [DefaultValue(true)]
    public bool HighEmphasis
    {
        get => _renderer.HighEmphasis;
        set => _renderer.HighEmphasis = value;
    }

    internal void ApplyTheme(HiveThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        var background = theme.Palette.ElevatedSurface;
        var foreground = theme.Palette.Text;
        var useAccentColor = false;
        var highEmphasis = false;
        var depth = 0;

        switch (_style)
        {
            case HiveButtonStyle.Primary:
                background = theme.Palette.Accent;
                foreground = theme.Palette.AccentForeground;
                useAccentColor = true;
                highEmphasis = true;
                depth = 1;
                break;

            case HiveButtonStyle.Secondary:
                break;

            case HiveButtonStyle.Navigation:
                background = theme.VisualStates.NavigationBackground;
                foreground = theme.VisualStates.NavigationText;
                break;

            case HiveButtonStyle.NavigationSelected:
                background = theme.VisualStates.NavigationSelected;
                foreground = theme.VisualStates.NavigationSelectedText;
                break;

            case HiveButtonStyle.Danger:
                background = theme.VisualStates.Error;
                foreground = theme.Palette.AccentForeground;
                highEmphasis = true;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(Style), _style, null);
        }

        if (!Enabled)
        {
            background = theme.Palette.DisabledBackground;
            foreground = theme.Palette.DisabledText;
            useAccentColor = false;
            highEmphasis = false;
            depth = 0;
        }

        _renderer.BackColor = background;
        _renderer.ForeColor = foreground;
        _renderer.UseAccentColor = useAccentColor;
        _renderer.HighEmphasis = highEmphasis && HighEmphasis && Enabled;
        _renderer.Depth = depth;
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        ApplyCurrentTheme();
    }

    private void ApplyCurrentTheme()
    {
        if (FindForm() is HiveForm form)
            ApplyTheme(form.ThemeManager.Theme);
    }
}
