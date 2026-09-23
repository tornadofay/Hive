using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

public sealed class HiveListView : ListView
{
    private const int RowHeight = 34;

    private HiveThemeDefinition? _theme;
    private int _hoverIndex = -1;
    private int _naturalLastColumnWidth = -1;
    private Font? _headerFont;
    private SolidBrush? _headerBackgroundBrush;
    private SolidBrush? _headerDisabledBrush;
    private SolidBrush? _rowInputBrush;
    private SolidBrush? _rowSurfaceBrush;
    private SolidBrush? _rowHoverBrush;
    private SolidBrush? _rowSelectionBrush;
    private SolidBrush? _rowDisabledBrush;
    private SolidBrush? _accentBrush;
    private Pen? _borderPen;
    private Pen? _focusPen;
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

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        FillLastColumn();
    }

    internal void ResetColumnLayout()
    {
        _naturalLastColumnWidth = -1;
        FillLastColumn();
    }

    internal void ApplyTheme(HiveThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        _theme = theme;

        ApplySurfaceTheme(theme);

        EnsureHeaderFont(theme);
        RebuildPaintResources(theme);

        var previous = _hoverIndex;
        _hoverIndex = -1;

        if (previous >= 0 && previous < Items.Count)
            Invalidate(GetItemRect(previous));

        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);

        _hoverIndex = -1;

        if (_theme is not null)
            ApplySurfaceTheme(_theme);

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

        var background = Enabled
            ? _headerBackgroundBrush
            : _headerDisabledBrush;
        var headerForeground = Enabled
            ? theme.Palette.Text
            : theme.Palette.DisabledText;

        if (background is not null)
            e.Graphics.FillRectangle(background, e.Bounds);

        if (e.ColumnIndex < Columns.Count - 1 && _borderPen is not null)
        {
            e.Graphics.DrawLine(
                _borderPen,
                e.Bounds.Right - 1,
                e.Bounds.Top + 5,
                e.Bounds.Right - 1,
                e.Bounds.Bottom - 5);
        }

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
            headerForeground,
            flags);

        if (_borderPen is not null)
        {
            e.Graphics.DrawLine(
                _borderPen,
                e.Bounds.Left,
                e.Bounds.Bottom - 1,
                e.Bounds.Right - 1,
                e.Bounds.Bottom - 1);
        }
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

        // Paint every visible sub-item cell. Painting the row only from column 0
        // leaves horizontally scrolled columns dependent on the native repaint path,
        // which can expose stale pixels at the right side of the list.
        var cell = e.Bounds;

        var background = !Enabled
            ? _rowDisabledBrush
            : selected
                ? _rowSelectionBrush
                : hovered
                    ? _rowHoverBrush
                    : item.Index % 2 == 0
                        ? _rowInputBrush
                        : _rowSurfaceBrush;

        if (background is not null)
            e.Graphics.FillRectangle(background, cell);

        if (_borderPen is not null)
        {
            e.Graphics.DrawLine(
                _borderPen,
                cell.Left,
                cell.Bottom - 1,
                cell.Right - 1,
                cell.Bottom - 1);
        }

        if (e.ColumnIndex == 0 && selected && Enabled && _accentBrush is not null)
        {
            e.Graphics.FillRectangle(
                _accentBrush,
                cell.Left,
                cell.Top + 5,
                3,
                Math.Max(8, cell.Height - 10));
        }

        if (e.ColumnIndex == 0 && selected && Focused && Enabled && _focusPen is not null)
        {
            var focus = Rectangle.Inflate(cell, -1, -1);
            e.Graphics.DrawRectangle(
                _focusPen,
                focus.Left,
                focus.Top,
                Math.Max(0, focus.Width - 1),
                Math.Max(0, focus.Height - 1));
        }

        var color = !Enabled
            ? theme.Palette.DisabledText
            : e.SubItem is { ForeColor.IsEmpty: false }
                ? e.SubItem.ForeColor
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

        var item = Enabled ? GetItemAt(e.X, e.Y) : null;
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

    private void ApplySurfaceTheme(HiveThemeDefinition theme)
    {
        // The list viewport is workspace, not an input. Rows provide their own
        // input/surface hierarchy, while the empty area remains aligned with the
        // surrounding CRUD content surface.
        var background = Enabled
            ? theme.Palette.Surface
            : theme.VisualStates.DisabledListBackground;

        var foreground = Enabled
            ? theme.Palette.Text
            : theme.Palette.DisabledText;

        if (BackColor != background)
            BackColor = background;

        if (ForeColor != foreground)
            ForeColor = foreground;
    }

    private void FillLastColumn()
    {
        if (Columns.Count == 0 || ClientSize.Width <= 0)
            return;

        var lastIndex = Columns.Count - 1;

        if (_naturalLastColumnWidth < 0)
            _naturalLastColumnWidth = Columns[lastIndex].Width;

        var precedingWidth = 0;
        for (var index = 0; index < lastIndex; index++)
            precedingWidth += Columns[index].Width;

        var availableWidth = ClientSize.Width - precedingWidth;
        var targetWidth = Math.Max(
            _naturalLastColumnWidth,
            Math.Max(1, availableWidth));

        if (Columns[lastIndex].Width == targetWidth)
            return;

        Columns[lastIndex].Width = targetWidth;
    }

    private void RebuildPaintResources(HiveThemeDefinition theme)
    {
        DisposePaintResources();

        _headerBackgroundBrush = new SolidBrush(theme.Palette.ElevatedSurface);
        _headerDisabledBrush = new SolidBrush(
            theme.VisualStates.DisabledListBackground);
        _rowInputBrush = new SolidBrush(theme.Palette.InputBackground);
        _rowSurfaceBrush = new SolidBrush(theme.Palette.Surface);
        _rowHoverBrush = new SolidBrush(theme.VisualStates.HoverBackground);
        _rowSelectionBrush = new SolidBrush(theme.Palette.Selection);
        _rowDisabledBrush = new SolidBrush(
            theme.VisualStates.DisabledListBackground);
        _accentBrush = new SolidBrush(theme.Palette.Accent);

        _borderPen = new Pen(theme.Palette.Border);
        _focusPen = new Pen(theme.VisualStates.FocusedBorder);
    }

    private void EnsureHeaderFont(HiveThemeDefinition theme)
    {
        var family = theme.Typography.FontFamily;

        var size = theme.Typography.SectionSize;

        if (_headerFont is not null &&
            string.Equals(_headerFont.FontFamily.Name, family, StringComparison.Ordinal) &&
            Math.Abs(_headerFont.Size - size) <= 0.01f)
        {
            return;
        }

        _headerFont?.Dispose();
        _headerFont = new Font(family, size, FontStyle.Bold);
    }

    private void DisposePaintResources()
    {
        _headerBackgroundBrush?.Dispose();
        _headerDisabledBrush?.Dispose();
        _rowInputBrush?.Dispose();
        _rowSurfaceBrush?.Dispose();
        _rowHoverBrush?.Dispose();
        _rowSelectionBrush?.Dispose();
        _rowDisabledBrush?.Dispose();
        _accentBrush?.Dispose();
        _borderPen?.Dispose();
        _focusPen?.Dispose();

        _headerBackgroundBrush = null;
        _headerDisabledBrush = null;
        _rowInputBrush = null;
        _rowSurfaceBrush = null;
        _rowHoverBrush = null;
        _rowSelectionBrush = null;
        _rowDisabledBrush = null;
        _accentBrush = null;
        _borderPen = null;
        _focusPen = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SmallImageList = null;
            _headerFont?.Dispose();
            DisposePaintResources();
            _rowImageList.Dispose();
        }

        base.Dispose(disposing);
    }
}
