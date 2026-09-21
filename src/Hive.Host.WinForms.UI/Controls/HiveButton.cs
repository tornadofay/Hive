using System.ComponentModel;
using System.Drawing;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Forms;
using ReaLTaiizor.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

public sealed class HiveButton : UserControl
{
    private readonly MaterialButton _renderer;

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
    }

    [Browsable(true)]
    [DefaultValue("Button")]
    [AllowNull]
    public override string Text
    {
        get => _renderer.Text;
        set => _renderer.Text = value ?? string.Empty;
    }

    [DefaultValue(true)]
    public bool HighEmphasis
    {
        get => _renderer.HighEmphasis;
        set => _renderer.HighEmphasis = value;
    }

    internal void ApplyTheme(HiveThemeDefinition theme)
    {
        _renderer.BackColor = theme.Palette.Accent;
        _renderer.ForeColor = theme.Palette.AccentForeground;
        _renderer.UseAccentColor = true;
    }
}
