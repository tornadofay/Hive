using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed class ScrollInfrastructureExampleView : UserControl
{
    private readonly IHiveThemeManager _themeManager;
    private readonly HiveScrollHost _scrollHost;
    private readonly Label _description;
    private readonly Label _state;
    private readonly Panel _contentSurface;
    private readonly Label _footer;
    private readonly Font _contentTitleFont;
    private readonly Font _markerTitleFont;

    public ScrollInfrastructureExampleView(
        IHiveThemeManager themeManager)
    {
        ArgumentNullException.ThrowIfNull(themeManager);

        _themeManager = themeManager;

        _contentTitleFont = new Font(
            themeManager.Theme.Typography.FontFamily,
            14f,
            FontStyle.Bold);
        _markerTitleFont = new Font(
            themeManager.Theme.Typography.FontFamily,
            10f,
            FontStyle.Bold);

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = new Padding(4, 0, 4, 4);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = new Padding(8),
        };
        root.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(
            new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(
            new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(
            new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(
            new RowStyle(SizeType.AutoSize));

        _description = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(960, 60),
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Text =
                "HiveScrollHost overlays HiveScrollBar controls around one caller-owned content surface. Scroll with the mouse wheel, drag a thumb, or focus a scrollbar and use its keyboard commands. Resize the window and switch themes to verify state preservation."
        };

        _state = new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 0),
            Padding = Padding.Empty
        };

        _scrollHost = new HiveScrollHost
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 12, 0, 0),
            MinimumSize = new Size(320, 260),
            AccessibleName = "Scroll infrastructure demonstration",
            AccessibleDescription =
                "Demonstrates Hive custom scrolling, wheel input, thumb interaction, keyboard scrolling, resize synchronization, and theme preservation."
        };

        _contentSurface = CreateContentSurface();
        _scrollHost.Attach(_contentSurface);
        _scrollHost.ScrollPositionChanged += ScrollHostOnScrollPositionChanged;

        _footer = new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 0),
            Padding = Padding.Empty,
            Text =
                "The sample content intentionally exceeds the viewport in both directions so both normalized scroll states become active."
        };

        root.Controls.Add(_description, 0, 0);
        root.Controls.Add(_state, 0, 1);
        root.Controls.Add(_scrollHost, 0, 2);
        root.Controls.Add(_footer, 0, 3);

        Controls.Add(root);

        UpdateStateText();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _scrollHost.ScrollPositionChanged -=
                ScrollHostOnScrollPositionChanged;
            _contentTitleFont.Dispose();
            _markerTitleFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private Panel CreateContentSurface()
    {
        var surface = new Panel
        {
            Size = new Size(1120, 860),
            Padding = new Padding(28),
            Margin = Padding.Empty,
            BackColor = _themeManager.Theme.Palette.ElevatedSurface
        };

        var title = new Label
        {
            AutoSize = true,
            Location = new Point(28, 28),
            Font = _contentTitleFont,
            Text = "Hive scroll viewport"
        };

        var description = new Label
        {
            AutoSize = true,
            Location = new Point(28, 68),
            MaximumSize = new Size(920, 48),
            Text =
                "This surface is intentionally larger than the visible host. The right edge and bottom edge contain markers so horizontal and vertical movement can be verified visually."
        };

        surface.Controls.Add(title);
        surface.Controls.Add(description);

        AddMarker(
            surface,
            new Point(28, 150),
            "Top-left",
            "Origin of the content surface.");

        AddMarker(
            surface,
            new Point(820, 150),
            "Top-right",
            "Horizontal scrolling reaches this region.");

        AddMarker(
            surface,
            new Point(28, 700),
            "Bottom-left",
            "Vertical scrolling reaches this region.");

        AddMarker(
            surface,
            new Point(820, 700),
            "Bottom-right",
            "Both axes can reach the far content extent.");

        var action = new HiveButton
        {
            Text = "Focusable content",
            Style = HiveButtonStyle.Secondary,
            Size = new Size(170, 36),
            Location = new Point(480, 380),
            AccessibleName = "Focusable content example",
            AccessibleDescription =
                "Focusable content inside the custom scroll host."
        };

        surface.Controls.Add(action);
        return surface;
    }

    private static void AddMarker(
        Panel surface,
        Point location,
        string title,
        string description)
    {
        var panel = new Panel
        {
            Size = new Size(270, 96),
            Location = location,
            Padding = new Padding(14)
        };

        var titleLabel = new Label
        {
            AutoSize = true,
            Text = title,
            Location = new Point(14, 12)
        };

        var descriptionLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(235, 52),
            Text = description,
            Location = new Point(14, 38)
        };

        panel.Controls.Add(titleLabel);
        panel.Controls.Add(descriptionLabel);
        surface.Controls.Add(panel);
    }

    private void ScrollHostOnScrollPositionChanged(
        object? sender,
        EventArgs e)
    {
        UpdateStateText();
    }

    private void UpdateStateText()
    {
        _state.Text =
            $"Horizontal: {_scrollHost.HorizontalScrollPosition} / " +
            $"{_scrollHost.HorizontalScrollState.EffectiveMaximum}    " +
            $"Vertical: {_scrollHost.VerticalScrollPosition} / " +
            $"{_scrollHost.VerticalScrollState.EffectiveMaximum}";
    }

}
