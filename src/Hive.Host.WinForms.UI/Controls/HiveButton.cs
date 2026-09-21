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
        Size = new Size(120, 40);
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

        switch (_style)
        {
            case HiveButtonStyle.Primary:
                _renderer.BackColor = theme.Palette.Accent;
                _renderer.ForeColor = theme.Palette.AccentForeground;
                _renderer.UseAccentColor = true;
                break;

            case HiveButtonStyle.Secondary:
                _renderer.BackColor = theme.Palette.Surface;
                _renderer.ForeColor = theme.Palette.Text;
                _renderer.UseAccentColor = false;
                break;

            case HiveButtonStyle.Navigation:
                _renderer.BackColor = theme.VisualStates.NavigationBackground;
                _renderer.ForeColor = theme.VisualStates.NavigationText;
                _renderer.UseAccentColor = false;
                break;

            case HiveButtonStyle.Danger:
                _renderer.BackColor = theme.VisualStates.Error;
                _renderer.ForeColor = theme.Palette.AccentForeground;
                _renderer.UseAccentColor = false;
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }

        _renderer.HighEmphasis = HighEmphasis;
    }

    private void ApplyCurrentTheme()
    {
        if (FindForm() is HiveForm form)
            ApplyTheme(form.ThemeManager.Theme);
    }
}
