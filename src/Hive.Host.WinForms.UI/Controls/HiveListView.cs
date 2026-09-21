using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

public sealed class HiveListView : ListView
{
    private const int RowHeight = 36;

    private HiveThemeDefinition? _theme;
    private int _hoverIndex = -1;
    private Font? _headerFont;
    private readonly ImageList _rowImageList;

    public HiveListView()
    {
        // ListView is a native Win32 control. Keep the native paint path intact and
        // use OwnerDraw for the item-specific visuals instead of forcing UserPaint.
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
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
            ImageSize = new Size(1, RowHeight)
        };
        _rowImageList.Images.Add(new Bitmap(1, RowHeight));
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

        var previous = _hoverIndex;
        _hoverIndex = -1;

        if (previous >= 0 && previous < Items.Count)
            Invalidate(GetItemRect(previous));

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

        using var bottom = new Pen(theme.Palette.Border);
        e.Graphics.DrawLine(
            bottom,
            e.Bounds.Left,
            e.Bounds.Bottom - 1,
            e.Bounds.Right - 1,
            e.Bounds.Bottom - 1);
    }

    protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
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

        // Draw the whole row from the first column only. ListView can issue an
        // extra DrawItem notification while hovering; keeping the row background
        // here prevents that native repaint from covering custom text.
        if (e.ColumnIndex == 0)
        {
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
                        : item.Index % 2 == 0
                            ? theme.Palette.InputBackground
                            : theme.Palette.Surface;

            using var brush = new SolidBrush(background);
            e.Graphics.FillRectangle(brush, row);

            using var separator = new Pen(theme.Palette.Border);
            e.Graphics.DrawLine(
                separator,
                row.Left,
                row.Bottom - 1,
                row.Right - 1,
                row.Bottom - 1);

            if (selected)
            {
                using var accent = new SolidBrush(theme.Palette.Accent);
                e.Graphics.FillRectangle(
                    accent,
                    row.Left,
                    row.Top + 5,
                    3,
                    Math.Max(8, row.Height - 10));
            }

            if (selected && Focused)
            {
                using var focusPen = new Pen(theme.VisualStates.FocusedBorder);
                var focus = Rectangle.Inflate(row, -1, -1);
                e.Graphics.DrawRectangle(
                    focusPen,
                    focus.Left,
                    focus.Top,
                    Math.Max(0, focus.Width - 1),
                    Math.Max(0, focus.Height - 1));
            }
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

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var item = GetItemAt(e.X, e.Y);
        var next = item?.Index ?? -1;
        if (_hoverIndex == next)
            return;

        var previous = _hoverIndex;
        _hoverIndex = next;

        if (previous >= 0 && previous < Items.Count)
            Invalidate(GetItemRect(previous));

        if (next >= 0 && next < Items.Count)
            Invalidate(GetItemRect(next));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);

        if (_hoverIndex < 0)
            return;

        var previous = _hoverIndex;
        _hoverIndex = -1;

        if (previous >= 0 && previous < Items.Count)
            Invalidate(GetItemRect(previous));
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
        if (disposing)
        {
            SmallImageList = null;
            _headerFont?.Dispose();
            _rowImageList.Dispose();
        }

        base.Dispose(disposing);
    }
}
