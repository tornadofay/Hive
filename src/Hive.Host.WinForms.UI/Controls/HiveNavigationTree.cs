using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

public sealed class HiveNavigationTree : TreeView
{
    private const int RowHeight = 34;
    private const int RowHorizontalPadding = 5;
    private const int RowVerticalPadding = 1;

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
        ShowPlusMinus = false;
        ShowRootLines = false;
        Indent = 18;
        ItemHeight = RowHeight;
        DoubleBuffered = true;
        Margin = Padding.Empty;
        Padding = new Padding(4, 8, 4, 8);
        AccessibleRole = AccessibleRole.Outline;
        AccessibleName = "Example navigation";
        AccessibleDescription =
            "Browse Hive examples by category, subcategory, and example.";
    }

    internal void ApplyTheme(HiveThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        _theme = theme;

        if (BackColor != theme.VisualStates.NavigationBackground)
            BackColor = theme.VisualStates.NavigationBackground;

        if (ForeColor != theme.VisualStates.NavigationText)
            ForeColor = theme.VisualStates.NavigationText;

        EnsureFonts(theme);

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

        var textColor = !Enabled
            ? theme.Palette.DisabledText
            : selected
                ? theme.VisualStates.NavigationSelectedText
                : theme.VisualStates.NavigationText;

        var textLeft = e.Bounds.Left + 4;

        if (e.Node.Nodes.Count > 0)
        {
            var glyphSize = 12;
            var glyphLeft = Math.Max(
                row.Left + 3,
                e.Bounds.Left - glyphSize - 5);
            var glyphTop = row.Top + Math.Max(
                0,
                (row.Height - glyphSize) / 2);

            using var glyphPen = new Pen(
                !Enabled
                    ? theme.Palette.DisabledText
                    : selected
                        ? theme.VisualStates.NavigationSelectedText
                        : theme.VisualStates.NavigationText,
                1.5f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var centerX = glyphLeft + glyphSize / 2f;
            var centerY = glyphTop + glyphSize / 2f;
            var half = 3.5f;

            e.Graphics.DrawLine(
                glyphPen,
                centerX - half,
                centerY,
                centerX + half,
                centerY);

            if (!e.Node.IsExpanded)
            {
                e.Graphics.DrawLine(
                    glyphPen,
                    centerX,
                    centerY - half,
                    centerX,
                    centerY + half);
            }

            textLeft = Math.Max(
                textLeft,
                glyphLeft + glyphSize + 5);
        }

        var textRectangle = new Rectangle(
            textLeft,
            row.Top,
            Math.Max(0, ClientSize.Width - textLeft - 12),
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

        if (Enabled && focused && selected)
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

        Invalidate();
    }

    protected override void OnAfterExpand(TreeViewEventArgs e)
    {
        base.OnAfterExpand(e);

        if (e.Node is TreeNode node)
            Invalidate(node.Bounds);
    }

    protected override void OnAfterCollapse(TreeViewEventArgs e)
    {
        base.OnAfterCollapse(e);

        if (e.Node is TreeNode node)
            Invalidate(node.Bounds);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (!Enabled || e.Button != MouseButtons.Left)
        {
            base.OnMouseDown(e);
            return;
        }

        var node = GetNodeAt(e.Location);
        if (node is not null && node.Nodes.Count > 0)
        {
            node.Toggle();
            Focus();
            return;
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var next = Enabled ? GetNodeAt(e.Location) : null;
        if (ReferenceEquals(next, _hoverNode))
            return;

        var previousNode = _hoverNode;
        _hoverNode = next;

        if (previousNode is not null)
            Invalidate(GetRowBounds(previousNode));

        if (next is not null)
            Invalidate(GetRowBounds(next));
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

    private Rectangle GetRowBounds(TreeNode node)
    {
        var row = node.Bounds;
        return new Rectangle(
            RowHorizontalPadding,
            row.Top + RowVerticalPadding,
            Math.Max(0, ClientSize.Width - RowHorizontalPadding * 2),
            Math.Max(1, row.Height - RowVerticalPadding * 2));
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

    private Rectangle GetGlyphBounds(TreeNode node)
    {
        const int glyphSize = 12;
        const int glyphGap = 5;

        var rowTop = node.Bounds.Top + RowVerticalPadding;
        var rowHeight = Math.Max(
            1,
            node.Bounds.Height - RowVerticalPadding * 2);

        var rowLeft = RowHorizontalPadding;
        var glyphLeft = Math.Max(
            rowLeft + 3,
            node.Bounds.Left - glyphSize - glyphGap);
        var glyphTop = rowTop + Math.Max(
            0,
            (rowHeight - glyphSize) / 2);

        return new Rectangle(
            glyphLeft - 3,
            glyphTop - 3,
            glyphSize + 6,
            glyphSize + 6);
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
