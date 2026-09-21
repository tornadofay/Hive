using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;
using System.ComponentModel;

namespace Hive.Host.WinForms.UI.Controls;

public sealed class HiveListPageLayout : UserControl
{
    private const int DefaultHeaderHeight = 70;
    private const int DefaultActionBarHeight = 50;

    private readonly TableLayoutPanel _layout;
    private readonly Panel _headerPanel;
    private readonly Panel _actionBarPanel;
    private readonly Panel _contentPanel;
    private int _headerHeight = DefaultHeaderHeight;
    private int _actionBarHeight = DefaultActionBarHeight;

    public HiveListPageLayout()
    {
        Dock = DockStyle.Fill;
        BackColor = SystemColors.Window;
        Padding = Padding.Empty;

        _layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        _headerPanel = CreateSurfacePanel(
            new Padding(12, 4, 12, 4));
        _actionBarPanel = CreateSurfacePanel(
            new Padding(12, 4, 12, 4));
        _contentPanel = CreateSurfacePanel(
            new Padding(12, 8, 12, 8));

        _layout.Controls.Add(_headerPanel, 0, 0);
        _layout.Controls.Add(_actionBarPanel, 0, 1);
        _layout.Controls.Add(_contentPanel, 0, 2);

        Controls.Add(_layout);
        UpdateRowHeights();
    }

    internal void ApplyTheme(HiveThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        BackColor = theme.Palette.Surface;
        _layout.BackColor = theme.Palette.Surface;
        _headerPanel.BackColor = theme.Palette.ElevatedSurface;
        _actionBarPanel.BackColor = theme.Palette.Surface;
        _contentPanel.BackColor = theme.Palette.WindowBackground;
    }

    public Panel HeaderPanel => _headerPanel;

    public Panel ActionBarPanel => _actionBarPanel;

    public Panel ContentPanel => _contentPanel;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int HeaderHeight
    {
        get => _headerHeight;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            if (_headerHeight == value)
                return;

            _headerHeight = value;
            UpdateRowHeights();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ActionBarHeight
    {
        get => _actionBarHeight;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            if (_actionBarHeight == value)
                return;

            _actionBarHeight = value;
            UpdateRowHeights();
        }
    }

    public void SetContent(Control content)
    {
        ArgumentNullException.ThrowIfNull(content);

        _contentPanel.SuspendLayout();
        try
        {
            _contentPanel.Controls.Clear();
            content.Dock = DockStyle.Fill;
            _contentPanel.Controls.Add(content);
        }
        finally
        {
            _contentPanel.ResumeLayout(true);
        }
    }

    private void UpdateRowHeights()
    {
        if (_layout.RowStyles.Count != 3)
            return;

        _layout.RowStyles[0].Height = _headerHeight;
        _layout.RowStyles[1].Height = _actionBarHeight;
        _layout.RowStyles[2].Height = 100f;
    }

    private static Panel CreateSurfacePanel(Padding padding) =>
        new()
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = padding
        };

    private static void ApplySurface(Control root, HiveThemeDefinition theme)
    {
        foreach (Control child in root.Controls)
        {
            if (child is Panel or UserControl)
                child.BackColor = theme.Palette.Surface;
        }
    }
}
