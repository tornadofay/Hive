using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

public sealed class HiveListView : ListView
{
    private HiveThemeDefinition? _theme;
    private int _hoverIndex = -1;
    private Font? _headerFont;

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
        BorderStyle = BorderStyle.FixedSingle;
        LabelWrap = false;
        Margin = Padding.Empty;
        DoubleBuffered = true;
    }

    internal void ApplyTheme(HiveThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        var topIndex = IsHandleCreated ? TopItem?.Index ?? -1 : -1;
        var selectedIndex = SelectedIndices.Count > 0
            ? SelectedIndices[0]
            : -1;

        _theme = theme;

        if (BackColor != theme.Palette.InputBackground)
            BackColor = theme.Palette.InputBackground;

        if (ForeColor != theme.Palette.Text)
            ForeColor = theme.Palette.Text;

        _headerFont?.Dispose();
        _headerFont = new Font(
            theme.Typography.FontFamily,
            8.8f,
            FontStyle.Bold);

        if (selectedIndex >= 0 && selectedIndex < Items.Count)
            Items[selectedIndex].Selected = true;

        if (topIndex >= 0 && topIndex < Items.Count)
            Items[topIndex].EnsureVisible();

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

        flags |= e.Header.TextAlign switch
        {
            HorizontalAlignment.Right => TextFormatFlags.Right,
            HorizontalAlignment.Center => TextFormatFlags.HorizontalCenter,
            _ => TextFormatFlags.Left
        };

        TextRenderer.DrawText(
            e.Graphics,
            e.Header.Text,
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

        var selected = e.Item.Selected;
        var hovered = e.Item.Index == _hoverIndex && !selected;
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
    }

    protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
    {
        var theme = _theme;
        if (theme is null)
        {
            e.DrawDefault = true;
            return;
        }

        var color = !Enabled || !e.Item.Enabled
            ? theme.Palette.DisabledText
            : e.Item.Selected
                ? theme.Palette.Text
                : theme.Palette.Text;

        var flags = TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPrefix;

        flags |= e.Header.TextAlign switch
        {
            HorizontalAlignment.Right => TextFormatFlags.Right,
            HorizontalAlignment.Center => TextFormatFlags.HorizontalCenter,
            _ => TextFormatFlags.Left
        };

        TextRenderer.DrawText(
            e.Graphics,
            e.SubItem.Text,
            Font,
            Rectangle.Inflate(e.Bounds, -10, 0),
            color,
            flags);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var item = GetItemAt(e.Location);
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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _headerFont?.Dispose();
    }
}
