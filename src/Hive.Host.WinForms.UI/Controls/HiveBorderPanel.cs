using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;
using System.ComponentModel;

namespace Hive.Host.WinForms.UI.Controls;

internal sealed class HiveBorderPanel : Panel
{
    private const int DefaultCornerRadius = 7;

    private HiveThemeDefinition? _theme;
    private Color? _borderColor;
    private int _cornerRadius = DefaultCornerRadius;
    public HiveBorderPanel()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        Margin = Padding.Empty;
        Padding = new Padding(1);
        BackColor = SystemColors.Window;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Color? BorderColor
    {
        get => _borderColor;
        set
        {
            if (_borderColor == value)
                return;

            _borderColor = value;
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal int CornerRadius
    {
        get => _cornerRadius;
        set
        {
            var next = Math.Max(0, value);
            if (_cornerRadius == next)
                return;

            _cornerRadius = next;
            RebuildRegion();
            Invalidate();
        }
    }

    internal void ApplyTheme(HiveThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        _theme = theme;

        if (BackColor != theme.Palette.Surface)
            BackColor = theme.Palette.Surface;

        Invalidate();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        RebuildRegion();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var theme = _theme;
        if (theme is null ||
            ClientSize.Width <= 1 ||
            ClientSize.Height <= 1)
            return;

        using var pen = new Pen(_borderColor ?? theme.Palette.Border, 1f)
        {
            Alignment = PenAlignment.Inset
        };

        using var path = CreateRoundedPath(
            new RectangleF(
                0.5f,
                0.5f,
                ClientSize.Width - 1f,
                ClientSize.Height - 1f),
            _cornerRadius);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        e.Graphics.DrawPath(pen, path);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            var previousRegion = Region;
            Region = null;
            previousRegion?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void RebuildRegion()
    {
        if (ClientSize.Width <= 1 || ClientSize.Height <= 1)
        {
            var oldRegion = Region;
            Region = null;
            oldRegion?.Dispose();
            return;
        }

        using var path = CreateRoundedPath(
            new RectangleF(
                0,
                0,
                ClientSize.Width,
                ClientSize.Height),
            _cornerRadius);

        var previousRegion = Region;
        Region = new Region(path);
        previousRegion?.Dispose();
    }

    private static GraphicsPath CreateRoundedPath(
        RectangleF bounds,
        int radius)
    {
        var path = new GraphicsPath();

        if (radius <= 0)
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
}
