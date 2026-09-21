using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

public sealed class HiveNavigationTree : TreeView
{
    private const int RowHeight = 32;
    private const int RowHorizontalPadding = 6;
    private const int RowVerticalPadding = 2;

    private HiveThemeDefinition? _theme;
    private TreeNode? _hoverNode;
    private Font? _categoryFont;
    private Font? _groupFont;
    private Font? _itemFont;

    public HiveNavigationTree()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        DrawMode = TreeViewDrawMode.OwnerDrawText;
        BorderStyle = BorderStyle.None;
        FullRowSelect = true;
        HideSelection = false;
        HotTracking = false;
        ShowLines = false;
        ShowPlusMinus = true;
        ShowRootLines = false;
        Indent = 20;
        ItemHeight = RowHeight;
        Margin = Padding.Empty;
        Padding = new Padding(4, 8, 4, 8);
    }

    internal void ApplyTheme(HiveThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        var selected = SelectedNode;
        var top = TopNode;
        var hadFocus = Focused;

        BeginUpdate();
        try
        {
            _theme = theme;

            if (BackColor != theme.VisualStates.NavigationBackground)
                BackColor = theme.VisualStates.NavigationBackground;

            if (ForeColor != theme.VisualStates.NavigationText)
                ForeColor = theme.VisualStates.NavigationText;

            RebuildFonts(theme);
            _hoverNode = null;
        }
        finally
        {
            EndUpdate();
        }

        if (selected is not null)
            SelectedNode = selected;

        if (top is not null)
            TopNode = top;

        if (hadFocus && CanFocus)
            Focus();

        Invalidate();
    }

    protected override void OnDrawNode(DrawTreeNodeEventArgs e)
    {
        var theme = _theme;
        if (theme is null)
        {
            base.OnDrawNode(e);
            return;
        }

        var selected = ReferenceEquals(e.Node, SelectedNode);
        var hovered = ReferenceEquals(e.Node, _hoverNode) && !selected;
        var row = new Rectangle(
            RowHorizontalPadding,
            e.Bounds.Top + RowVerticalPadding,
            Math.Max(0, ClientSize.Width - RowHorizontalPadding * 2),
            Math.Max(1, e.Bounds.Height - RowVerticalPadding * 2));

        using (var background = new SolidBrush(
                   selected
                       ? theme.VisualStates.NavigationSelected
                       : hovered
                           ? theme.VisualStates.NavigationHover
                           : theme.VisualStates.NavigationBackground))
        {
            e.Graphics.FillRectangle(background, row);
        }

        if (selected)
        {
            using var accent = new SolidBrush(theme.Palette.Accent);
            e.Graphics.FillRectangle(
                accent,
                row.Left,
                row.Top + 6,
                3,
                Math.Max(8, row.Height - 12));
        }

        var font = e.Node.Level switch
        {
            0 => _categoryFont ?? Font,
            1 => _groupFont ?? Font,
            _ => _itemFont ?? Font
        };

        var textColor = selected
            ? theme.VisualStates.NavigationSelectedText
            : e.Node.Level == 0
                ? theme.VisualStates.NavigationText
                : theme.VisualStates.NavigationText;

        var textRectangle = new Rectangle(
            e.Bounds.Left + 4,
            row.Top,
            Math.Max(0, ClientSize.Width - e.Bounds.Left - 12),
            row.Height);

        TextRenderer.DrawText(
            e.Graphics,
            e.Node.Text,
            font,
            textRectangle,
            textColor,
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix);

        if (Focused && selected)
        {
            using var focusPen = new Pen(theme.Palette.Accent, 1f);
            var focusRectangle = Rectangle.Inflate(row, -1, -1);
            using var path = CreateRoundedPath(focusRectangle, 6);
            e.Graphics.DrawPath(focusPen, path);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var next = GetNodeAt(e.Location);
        if (ReferenceEquals(next, _hoverNode))
            return;

        _hoverNode = next;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);

        if (_hoverNode is null)
            return;

        _hoverNode = null;
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _categoryFont?.Dispose();
            _groupFont?.Dispose();
            _itemFont?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void RebuildFonts(HiveThemeDefinition theme)
    {
        _categoryFont?.Dispose();
        _groupFont?.Dispose();
        _itemFont?.Dispose();

        _categoryFont = new Font(
            theme.Typography.FontFamily,
            9.5f,
            FontStyle.Bold);

        _groupFont = new Font(
            theme.Typography.FontFamily,
            9.1f,
            FontStyle.Bold);

        _itemFont = new Font(
            theme.Typography.FontFamily,
            theme.Typography.BodySize,
            FontStyle.Regular);
    }

    private static GraphicsPath CreateRoundedPath(
        Rectangle bounds,
        int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();

        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180f, 90f);
        path.AddArc(
            bounds.Right - diameter,
            bounds.Y,
            diameter,
            diameter,
            270f,
            90f);
        path.AddArc(
            bounds.Right - diameter,
            bounds.Bottom - diameter,
            diameter,
            diameter,
            0f,
            90f);
        path.AddArc(
            bounds.X,
            bounds.Bottom - diameter,
            diameter,
            diameter,
            90f,
            90f);
        path.CloseFigure();

        return path;
    }
}
