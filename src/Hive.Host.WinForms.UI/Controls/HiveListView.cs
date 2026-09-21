using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

public sealed class HiveListView : ListView
{
    private HiveThemeDefinition? _theme;
    private int _hoverIndex = -1;
    private Font? _headerFont;
    private readonly ImageList _rowImageList;

    public HiveListView()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer,
            true);

        OwnerDraw = true;
        View = View.Details;
        FullRowSelect = true;
        HideSelection = false;
        GridLines = false;
        MultiSelect = false;
        HeaderStyle = ColumnHeaderStyle.Nonclickable;
        BorderStyle = BorderStyle.None;
        LabelWrap = false;
        Margin = Padding.Empty;
        DoubleBuffered = true;

        _rowImageList = new ImageList
        {
            ColorDepth = ColorDepth.Depth32Bit,
            ImageSize = new Size(1, 36)
        };
        _rowImageList.Images.Add(new Bitmap(1, 36));
        SmallImageList = _rowImageList;
    }

    internal void ApplyTheme(HiveThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        _theme = theme;

        if (BackColor != theme.Palette.InputBackground)
            BackColor = theme.Palette.InputBackground;

        if (ForeColor != theme.Palette.Text)
            ForeColor = theme.Palette.Text;

        EnsureHeaderFont(theme);
        _hoverIndex = -1;
        Invalidate();
    }

    protected override void OnDrawColumnHeader(
        DrawListViewColumnHeaderEventArgs e)
    {
        var theme = _theme;
        if (theme is null)
        {
            e.DrawDefault = true;
            return;
        }

        using var background = new SolidBrush(theme.Palette.ElevatedSurface);
        e.Graphics.FillRectangle(background, e.Bounds);

        using var divider = new Pen(theme.Palette.Border);
        e.Graphics.DrawLine(
            divider,
            e.Bounds.Right - 1,
            e.Bounds.Top + 5,
            e.Bounds.Right - 1,
            e.Bounds.Bottom - 5);

        var flags = TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPrefix;

        var header = e.Header;
        if (header is null)
        {
            e.DrawDefault = true;
            return;
        }

        flags |= header.TextAlign switch
        {
            HorizontalAlignment.Right => TextFormatFlags.Right,
            HorizontalAlignment.Center => TextFormatFlags.HorizontalCenter,
            _ => TextFormatFlags.Left
        };

        TextRenderer.DrawText(
            e.Graphics,
            header.Text,
            _headerFont ?? Font,
            Rectangle.Inflate(e.Bounds, -10, 0),
            theme.Palette.Text,
            flags);
    }

    protected override void OnDrawItem(DrawListViewItemEventArgs e)
    {
        var theme = _theme;
        if (theme is null)
        {
            e.DrawDefault = true;
            return;
        }

        var item = e.Item;
        if (item is null)
        {
            e.DrawDefault = true;
            return;
        }

        var selected = item.Selected;
        var hovered = item.Index == _hoverIndex && !selected;
        var row = new Rectangle(
            1,
            e.Bounds.Top,
            Math.Max(0, ClientSize.Width - 2),
            e.Bounds.Height);

        var background = !Enabled
            ? theme.Palette.DisabledBackground
            : selected
                ? theme.Palette.Selection
                : hovered
                    ? theme.VisualStates.HoverBackground
                    : theme.Palette.InputBackground;

        using var brush = new SolidBrush(background);
        e.Graphics.FillRectangle(brush, row);

        using var separator = new Pen(theme.Palette.Border);
        e.Graphics.DrawLine(
            separator,
            row.Left,
            row.Bottom - 1,
            row.Right - 1,
            row.Bottom - 1);
    }

    protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
    {
        var theme = _theme;
        if (theme is null)
        {
            e.DrawDefault = true;
            return;
        }

        var color = !Enabled
            ? theme.Palette.DisabledText
            : theme.Palette.Text;

        var flags = TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPrefix;

        var header = e.Header;
        var subItem = e.SubItem;
        if (header is null || subItem is null)
        {
            e.DrawDefault = true;
            return;
        }

        flags |= header.TextAlign switch
        {
            HorizontalAlignment.Right => TextFormatFlags.Right,
            HorizontalAlignment.Center => TextFormatFlags.HorizontalCenter,
            _ => TextFormatFlags.Left
        };

        TextRenderer.DrawText(
            e.Graphics,
            subItem.Text,
            Font,
            Rectangle.Inflate(e.Bounds, -10, 0),
            color,
            flags);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var theme = _theme;
        if (theme is null)
            return;

        var border = ClientRectangle;
        border.Width -= 1;
        border.Height -= 1;
        if (border.Width <= 0 || border.Height <= 0)
            return;

        using var pen = new Pen(theme.Palette.Border);
        e.Graphics.DrawRectangle(pen, border);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var item = GetItemAt(e.X, e.Y);
        var next = item?.Index ?? -1;
        if (_hoverIndex == next)
            return;

        _hoverIndex = next;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);

        if (_hoverIndex < 0)
            return;

        _hoverIndex = -1;
        Invalidate();
    }

    private void EnsureHeaderFont(HiveThemeDefinition theme)
    {
        var family = theme.Typography.FontFamily;

        if (_headerFont is not null &&
            string.Equals(_headerFont.FontFamily.Name, family, StringComparison.Ordinal) &&
            Math.Abs(_headerFont.Size - 8.8f) <= 0.01f)
        {
            return;
        }

        _headerFont?.Dispose();
        _headerFont = new Font(family, 8.8f, FontStyle.Bold);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _headerFont?.Dispose();
            _rowImageList.Dispose();
        }
    }
}
