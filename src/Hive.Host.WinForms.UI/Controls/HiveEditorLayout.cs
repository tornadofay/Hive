using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace Hive.Host.WinForms.UI.Controls;

public sealed class HiveEditorLayout : UserControl
{
    private const int DefaultLabelColumnWidth = 190;
    private const int DefaultFieldHeight = 68;
    private const int FooterHeight = 54;
    private const int FooterSeparatorHeight = 1;

    private readonly TableLayoutPanel _root;
    private readonly TableLayoutPanel _fields;
    private readonly TableLayoutPanel _footerRoot;
    private readonly FlowLayoutPanel _footer;
    private readonly Panel _footerSeparator;
    private readonly Font _descriptionFont;
    private readonly Font _titleFont;
    private int _labelColumnWidth = DefaultLabelColumnWidth;

    public HiveEditorLayout()
    {
        _descriptionFont = new Font("Segoe UI", 8.5f);
        _titleFont = new Font("Segoe UI Semibold", 9.4f, FontStyle.Bold);

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, FooterHeight));

        _fields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            ColumnCount = 2,
            RowCount = 0,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _fields.ColumnStyles.Add(
            new ColumnStyle(SizeType.Absolute, _labelColumnWidth));
        _fields.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100f));

        _footerRoot = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _footerRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _footerRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, FooterSeparatorHeight));
        _footerRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        _footerSeparator = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        _footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = new Padding(0, 8, 0, 8)
        };

        _footerRoot.Controls.Add(_footerSeparator, 0, 0);
        _footerRoot.Controls.Add(_footer, 0, 1);
        _root.Controls.Add(_fields, 0, 0);
        _root.Controls.Add(_footerRoot, 0, 1);
        Controls.Add(_root);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _descriptionFont.Dispose();
            _titleFont.Dispose();
        }

        base.Dispose(disposing);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int LabelColumnWidth
    {
        get => _labelColumnWidth;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            if (_labelColumnWidth == value)
                return;

            _labelColumnWidth = value;
            _fields.ColumnStyles[0].Width = value;
        }
    }

    public TableLayoutPanel FieldsPanel => _fields;

    public FlowLayoutPanel FooterPanel => _footer;

    public void ClearFields()
    {
        _fields.Controls.Clear();
        _fields.RowStyles.Clear();
        _fields.RowCount = 0;
    }

    public void AddField(
        string title,
        string description,
        Control editor,
        int height = DefaultFieldHeight)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Field title is required.", nameof(title));

        ArgumentNullException.ThrowIfNull(editor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var row = _fields.RowCount;
        _fields.RowCount++;

        if (_fields.RowStyles.Count <= row)
        {
            _fields.RowStyles.Add(
                new RowStyle(SizeType.Absolute, height));
        }
        else
        {
            _fields.RowStyles[row].SizeType = SizeType.Absolute;
            _fields.RowStyles[row].Height = height;
        }

        _fields.Controls.Add(
            CreateLabelPanel(title, description),
            0,
            row);

        var editorHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 10, 0, 8),
            Margin = Padding.Empty
        };

        editor.Dock = DockStyle.Fill;
        editorHost.Controls.Add(editor);
        _fields.Controls.Add(editorHost, 1, row);
    }

    public HiveButton AddActionButton(
        string text,
        HiveButtonStyle style = HiveButtonStyle.Secondary,
        int width = 104)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);

        var button = new HiveButton
        {
            Text = text,
            Style = style,
            Width = width,
            Height = 36,
            Margin = new Padding(8, 0, 0, 0)
        };
        _footer.Controls.Add(button);
        return button;
    }

    private Panel CreateLabelPanel(
        string title,
        string description)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 10, 18, 0),
            Margin = Padding.Empty
        };

        var descriptionLabel = new Label
        {
            Text = description ?? string.Empty,
            Dock = DockStyle.Fill,
            Font = _descriptionFont,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 22,
            Font = _titleFont,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        panel.Controls.Add(descriptionLabel);
        panel.Controls.Add(titleLabel);
        return panel;
    }
}
