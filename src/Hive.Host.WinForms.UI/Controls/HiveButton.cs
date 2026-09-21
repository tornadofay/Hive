using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
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
    private const int CornerRadius = 8;
    private const int BorderWidth = 1;
    private const int FocusBorderWidth = 2;

    private HiveButtonStyle _style = HiveButtonStyle.Primary;
    private HiveThemeDefinition? _theme;
    private bool _hovered;
    private bool _pressed;
    private GraphicsPath? _path;

    public HiveButton()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);

        MinimumSize = new Size(88, 36);
        Size = new Size(120, 38);
        Margin = Padding.Empty;
        Padding = Padding.Empty;
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.PushButton;
        Text = "Button";
    }

    [Browsable(true)]
    [DefaultValue("Button")]
    [AllowNull]
    public override string Text
    {
        get => base.Text;
        set
        {
            base.Text = value ?? string.Empty;
            Invalidate();
        }
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
            Invalidate();
            ApplyCurrentTheme();
        }
    }

    [DefaultValue(true)]
    public bool HighEmphasis { get; set; } = true;

    internal void ApplyTheme(HiveThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        _theme = theme;
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);

        _hovered = false;
        _pressed = false;
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        RebuildPath();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);

        if (!Enabled)
            return;

        _hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);

        _hovered = false;
        _pressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (!Enabled || e.Button != MouseButtons.Left)
            return;

        Focus();
        _pressed = true;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button != MouseButtons.Left)
            return;

        _pressed = false;
        Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!Enabled)
            return;

        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            _pressed = true;
            Invalidate();
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);

        if (!Enabled)
            return;

        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            _pressed = false;
            Invalidate();
            OnClick(EventArgs.Empty);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (_path is null ||
            ClientSize.Width <= 1 ||
            ClientSize.Height <= 1)
            return;

        var theme = _theme;
        if (theme is null)
        {
            using var fallbackBrush = new SolidBrush(SystemColors.Control);
            e.Graphics.FillPath(fallbackBrush, _path);
            DrawText(e.Graphics, SystemColors.ControlText);
            return;
        }

        var colors = ResolveColors(theme);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        using var fill = new SolidBrush(colors.Background);
        e.Graphics.FillPath(fill, _path);

        using var border = new Pen(
            Focused ? colors.FocusBorder : colors.Border,
            Focused ? FocusBorderWidth : BorderWidth)
        {
            Alignment = PenAlignment.Inset
        };
        e.Graphics.DrawPath(border, _path);

        DrawText(e.Graphics, colors.Foreground);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Enter or Keys.Space ||
        base.IsInputKey(keyData);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _path?.Dispose();
            _path = null;
        }

        base.Dispose(disposing);
    }

    private void DrawText(Graphics graphics, Color color)
    {
        var bounds = ClientRectangle;
        bounds.Inflate(-10, -2);

        TextRenderer.DrawText(
            graphics,
            Text,
            Font,
            bounds,
            color,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix);
    }

    private ButtonColors ResolveColors(HiveThemeDefinition theme)
    {
        if (!Enabled)
        {
            return new ButtonColors(
                theme.Palette.DisabledBackground,
                theme.Palette.DisabledText,
                theme.VisualStates.DisabledBorder,
                theme.VisualStates.DisabledBorder);
        }

        var background = theme.Palette.ElevatedSurface;
        var foreground = theme.Palette.Text;
        var border = theme.Palette.Border;
        var hover = theme.VisualStates.HoverBackground;
        var pressed = theme.VisualStates.PressedBackground;

        switch (_style)
        {
            case HiveButtonStyle.Primary:
                background = theme.Palette.Accent;
                hover = theme.Palette.AccentHover;
                foreground = theme.Palette.AccentForeground;
                border = theme.Palette.AccentHover;
                break;

            case HiveButtonStyle.Secondary:
                break;

            case HiveButtonStyle.Navigation:
                background = theme.VisualStates.NavigationBackground;
                hover = theme.VisualStates.NavigationHover;
                pressed = theme.VisualStates.NavigationPressed;
                foreground = theme.VisualStates.NavigationText;
                border = theme.VisualStates.NavigationBorder;
                break;

            case HiveButtonStyle.NavigationSelected:
                background = theme.VisualStates.NavigationSelected;
                hover = theme.VisualStates.NavigationSelected;
                pressed = theme.VisualStates.NavigationPressed;
                foreground = theme.VisualStates.NavigationSelectedText;
                border = theme.Palette.Accent;
                break;

            case HiveButtonStyle.Danger:
                background = theme.VisualStates.Error;
                hover = ControlPaint.Light(theme.VisualStates.Error, 0.08f);
                pressed = ControlPaint.Dark(theme.VisualStates.Error, 0.08f);
                foreground = theme.Palette.AccentForeground;
                border = theme.VisualStates.Error;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(Style), _style, null);
        }

        if (_pressed)
            background = pressed;
        else if (_hovered)
            background = hover;

        var focusBorder = theme.Palette.Accent;
        if (_style == HiveButtonStyle.Danger)
            focusBorder = theme.VisualStates.Error;

        return new ButtonColors(
            background,
            foreground,
            border,
            focusBorder);
    }

    private void RebuildPath()
    {
        _path?.Dispose();
        _path = null;

        if (ClientSize.Width <= 1 || ClientSize.Height <= 1)
            return;

        var radius = Math.Min(
            CornerRadius,
            Math.Min(ClientSize.Width, ClientSize.Height) / 2);

        _path = CreateRoundedPath(
            new RectangleF(
                0.5f,
                0.5f,
                ClientSize.Width - 1f,
                ClientSize.Height - 1f),
            radius);
    }

    private void ApplyCurrentTheme()
    {
        if (FindForm() is HiveForm form)
            ApplyTheme(form.ThemeManager.Theme);
    }

    private static GraphicsPath CreateRoundedPath(
        RectangleF bounds,
        float radius)
    {
        var path = new GraphicsPath();

        if (radius <= 0f)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var diameter = Math.Min(
            radius * 2f,
            Math.Min(bounds.Width, bounds.Height));

        var arc = new RectangleF(
            bounds.Left,
            bounds.Top,
            diameter,
            diameter);

        path.AddArc(arc, 180f, 90f);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270f, 90f);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0f, 90f);
        arc.X = bounds.Left;
        path.AddArc(arc, 90f, 90f);
        path.CloseFigure();

        return path;
    }

    private readonly record struct ButtonColors(
        Color Background,
        Color Foreground,
        Color Border,
        Color FocusBorder);
}
