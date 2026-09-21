using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

public class HivePage : UserControl
{
    private const int DefaultHeaderHeight = 64;
    private const int DefaultActionBarHeight = 46;

    private readonly TableLayoutPanel _layout;
    private readonly Panel _headerPanel;
    private readonly Panel _actionBarPanel;
    private readonly Panel _contentPanel;
    private readonly Label _titleLabel;
    private readonly Label _descriptionLabel;
    private readonly Font _titleFont;
    private readonly Font _descriptionFont;

    private int _headerHeight = DefaultHeaderHeight;
    private int _actionBarHeight = DefaultActionBarHeight;

    public HivePage()
    {
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _titleFont = new Font(
            "Segoe UI Semibold",
            15f,
            FontStyle.Bold);
        _descriptionFont = new Font(
            "Segoe UI",
            8.9f);

        _layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _layout.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100f));
        _layout.RowStyles.Add(
            new RowStyle(SizeType.Absolute, _headerHeight));
        _layout.RowStyles.Add(
            new RowStyle(SizeType.Absolute, _actionBarHeight));
        _layout.RowStyles.Add(
            new RowStyle(SizeType.Percent, 100f));

        _headerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(12, 5, 12, 4)
        };

        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        headerLayout.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100f));
        headerLayout.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 23));
        headerLayout.RowStyles.Add(
            new RowStyle(SizeType.Percent, 100f));

        _titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Font = _titleFont,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Text = "Page",
            TextAlign = ContentAlignment.MiddleLeft
        };

        _descriptionLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Font = _descriptionFont,
            Margin = new Padding(0, 2, 0, 0),
            Padding = Padding.Empty,
            TextAlign = ContentAlignment.TopLeft
        };

        headerLayout.Controls.Add(_titleLabel, 0, 0);
        headerLayout.Controls.Add(_descriptionLabel, 0, 1);
        _headerPanel.Controls.Add(headerLayout);

        _actionBarPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(12, 4, 12, 4)
        };

        _contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(12, 8, 12, 8)
        };

        _layout.Controls.Add(_headerPanel, 0, 0);
        _layout.Controls.Add(_actionBarPanel, 0, 1);
        _layout.Controls.Add(_contentPanel, 0, 2);

        Controls.Add(_layout);
        UpdateRowHeights();
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Title
    {
        get => _titleLabel.Text;
        set => _titleLabel.Text = value ?? string.Empty;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Description
    {
        get => _descriptionLabel.Text;
        set => _descriptionLabel.Text = value ?? string.Empty;
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

    internal void ApplyTheme(HiveThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        BackColor = theme.Palette.Surface;
        _layout.BackColor = theme.Palette.Surface;
        _headerPanel.BackColor = theme.Palette.ElevatedSurface;
        _actionBarPanel.BackColor = theme.Palette.Surface;
        _contentPanel.BackColor = theme.Palette.WindowBackground;

        _titleLabel.ForeColor = theme.Palette.Text;
        _descriptionLabel.ForeColor = theme.Palette.MutedText;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont.Dispose();
            _descriptionFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private void UpdateRowHeights()
    {
        if (_layout.RowStyles.Count != 3)
            return;

        _layout.RowStyles[0].Height = _headerHeight;
        _layout.RowStyles[1].Height = _actionBarHeight;
        _layout.RowStyles[2].Height = 100f;
    }
}
