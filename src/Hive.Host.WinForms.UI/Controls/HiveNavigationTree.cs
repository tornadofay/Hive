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

        BeginUpdate();
        try
        {
            _theme = theme;

            if (BackColor != theme.VisualStates.NavigationBackground)
                BackColor = theme.VisualStates.NavigationBackground;

            if (ForeColor != theme.VisualStates.NavigationText)
                ForeColor = theme.VisualStates.NavigationText;

            EnsureFonts(theme);
        }
        finally
        {
            EndUpdate();
        }

        // Theme changes repaint the existing native tree only. Do not reassign
        // selection or TopNode here: those assignments can cause the native TreeView
        // to scroll or re-center even when its state did not actually change.
        Invalidate();
    }

    protected override void OnDrawNode(DrawTreeNodeEventArgs e)
    {
        var theme = _theme;
        if (theme is null || e.Node is null)
        {
            base.OnDrawNode(e);
            return;
        }

        var selected = (e.State & TreeNodeStates.Selected) != 0;
        var focused = (e.State & TreeNodeStates.Focused) != 0;
        var hovered = Enabled &&
                      ReferenceEquals(e.Node, _hoverNode) &&
                      !selected;
        var row = new Rectangle(
            RowHorizontalPadding,
            e.Bounds.Top + RowVerticalPadding,
            Math.Max(0, ClientSize.Width - RowHorizontalPadding * 2),
            Math.Max(1, e.Bounds.Height - RowVerticalPadding * 2));

        using (var background = new SolidBrush(
                   !Enabled
                       ? theme.Palette.DisabledBackground
                       : selected
                           ? theme.VisualStates.NavigationSelected
                           : hovered
                               ? theme.VisualStates.NavigationHover
                               : theme.VisualStates.NavigationBackground))
        {
            e.Graphics.FillRectangle(background, row);
        }

        if (selected && Enabled)
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
            : Enabled
                ? theme.VisualStates.NavigationText
                : theme.Palette.DisabledText;

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

        if (focused && selected)
        {
            using var focusPen = new Pen(theme.Palette.Accent, 1f);
            var focusRectangle = Rectangle.Inflate(row, -1, -1);
            using var path = CreateRoundedPath(focusRectangle, 6);
            e.Graphics.DrawPath(focusPen, path);
        }
    }

    protected override void OnAfterSelect(TreeViewEventArgs e)
    {
        base.OnAfterSelect(e);

        // The native TreeView can update selection state and paint in separate
        // messages. Repaint once after the selection settles so old/new states
        // cannot remain visually stale.
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var next = Enabled ? GetNodeAt(e.Location) : null;
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
        base.Dispose(disposing);

        if (disposing)
        {
            _categoryFont?.Dispose();
            _groupFont?.Dispose();
            _itemFont?.Dispose();
        }
    }

    private void EnsureFonts(HiveThemeDefinition theme)
    {
        var family = theme.Typography.FontFamily;
        var bodySize = theme.Typography.BodySize;
        var categorySize = bodySize + 0.5f;

        if (FontMatches(_categoryFont, family, categorySize, FontStyle.Bold) &&
            FontMatches(_groupFont, family, bodySize, FontStyle.Bold) &&
            FontMatches(_itemFont, family, bodySize, FontStyle.Regular))
        {
            return;
        }

        Font? categoryFont = null;
        Font? groupFont = null;
        Font? itemFont = null;

        try
        {
            categoryFont = new Font(family, categorySize, FontStyle.Bold);
            groupFont = new Font(family, bodySize, FontStyle.Bold);
            itemFont = new Font(family, bodySize, FontStyle.Regular);
        }
        catch
        {
            categoryFont?.Dispose();
            groupFont?.Dispose();
            itemFont?.Dispose();
            throw;
        }

        var previousCategoryFont = _categoryFont;
        var previousGroupFont = _groupFont;
        var previousItemFont = _itemFont;

        _categoryFont = categoryFont;
        _groupFont = groupFont;
        _itemFont = itemFont;

        previousCategoryFont?.Dispose();
        previousGroupFont?.Dispose();
        previousItemFont?.Dispose();
    }

    private static bool FontMatches(
        Font? font,
        string family,
        float size,
        FontStyle style)
    {
        return font is not null &&
               string.Equals(font.FontFamily.Name, family, StringComparison.Ordinal) &&
               Math.Abs(font.Size - size) <= 0.01f &&
               font.Style == style;
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
