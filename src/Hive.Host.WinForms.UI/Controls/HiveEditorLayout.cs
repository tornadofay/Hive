using Hive.Host.WinForms.UI.Theme;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace Hive.Host.WinForms.UI.Controls;

public sealed class HiveEditorLayout : UserControl
{
    private const int DefaultLabelColumnWidth = 168;
    private const int DefaultFieldHeight = 72;
    private const int FooterHeight = 58;
    private const int FooterSeparatorHeight = 1;

    private readonly TableLayoutPanel _root;
    private readonly TableLayoutPanel _fields;
    private readonly TableLayoutPanel _footerRoot;
    private readonly FlowLayoutPanel _footer;
    private readonly Panel _footerSeparator;
    private Font _descriptionFont;
    private Font _titleFont;
    private readonly List<Label> _descriptionLabels = new();
    private readonly List<Label> _titleLabels = new();
    private readonly ToolTip _descriptionToolTip;
    private int _labelColumnWidth = DefaultLabelColumnWidth;
    private bool _layoutInitialized;

    public HiveEditorLayout()
    {
        var fallbackFont = SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;

        _descriptionFont = new Font(
            fallbackFont.FontFamily,
            fallbackFont.Size);
        _titleFont = new Font(
            fallbackFont.FontFamily,
            fallbackFont.Size,
            FontStyle.Bold);

        _descriptionToolTip = new ToolTip
        {
            AutoPopDelay = 8000,
            InitialDelay = 500,
            ReshowDelay = 200,
            ShowAlways = false
        };

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
        _layoutInitialized = true;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        if (!_layoutInitialized ||
            _fields.ColumnStyles.Count == 0)
            return;

        var width = ClientSize.Width;
        var target = width < 560
            ? 112
            : width < 720
                ? 136
                : DefaultLabelColumnWidth;

        if (_labelColumnWidth == target)
            return;

        _labelColumnWidth = target;
        _fields.ColumnStyles[0].Width = target;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _descriptionToolTip.Dispose();
            _descriptionFont.Dispose();
            _titleFont.Dispose();
        }
    }

    internal void ApplyTheme(HiveThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        EnsureTypography(theme);

        _root.BackColor = theme.Palette.Surface;
        _fields.BackColor = theme.Palette.Surface;
        _footerRoot.BackColor = theme.Palette.ElevatedSurface;
        _footer.BackColor = theme.Palette.ElevatedSurface;
        _footerSeparator.BackColor = theme.Palette.Border;

        foreach (var label in _titleLabels)
            label.ForeColor = theme.Palette.Text;

        foreach (var label in _descriptionLabels)
            label.ForeColor = theme.Palette.MutedText;

        Invalidate(true);
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
        _descriptionLabels.Clear();
        _titleLabels.Clear();
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

        var minimumEditorHeight = Math.Max(
            editor.MinimumSize.Height,
            editor.PreferredSize.Height);
        var resolvedHeight = Math.Max(
            height,
            minimumEditorHeight + 20);

        if (_fields.RowStyles.Count <= row)
        {
            _fields.RowStyles.Add(
                new RowStyle(SizeType.Absolute, resolvedHeight));
        }
        else
        {
            _fields.RowStyles[row].SizeType = SizeType.Absolute;
            _fields.RowStyles[row].Height = resolvedHeight;
        }

        _fields.Controls.Add(
            CreateLabelPanel(title, description),
            0,
            row);

        var editorHost = CreateEditorHost(editor);

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

    private static Control CreateEditorHost(Control editor)
    {
        ArgumentNullException.ThrowIfNull(editor);

        if (!IsCompactEditor(editor))
        {
            var host = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 10, 0, 10),
                Margin = Padding.Empty
            };

            editor.Dock = DockStyle.Fill;
            host.Controls.Add(editor);
            return host;
        }

        var editorHeight = Math.Max(
            32,
            editor.PreferredSize.Height);

        var compactHost = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        compactHost.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100f));
        compactHost.RowStyles.Add(
            new RowStyle(SizeType.Percent, 50f));
        compactHost.RowStyles.Add(
            new RowStyle(SizeType.Absolute, editorHeight));
        compactHost.RowStyles.Add(
            new RowStyle(SizeType.Percent, 50f));

        // Single-line native WinForms controls such as TextBox calculate their own
        // height when AutoSize remains enabled, which can override the shared 32px
        // compact-editor contract during layout.
        editor.AutoSize = false;
        editor.Dock = DockStyle.Fill;
        editor.Height = editorHeight;
        compactHost.Controls.Add(editor, 0, 1);
        return compactHost;
    }

    private static bool IsCompactEditor(Control editor) =>
        editor switch
        {
            TextBoxBase textBox => !textBox.Multiline,
            ComboBox => true,
            NumericUpDown => true,
            DomainUpDown => true,
            DateTimePicker => true,
            CheckBox => true,
            RadioButton => true,
            LinkLabel => true,
            _ => false
        };

    private void EnsureTypography(HiveThemeDefinition theme)
    {
        var family = theme.Typography.FontFamily;
        var descriptionSize = theme.Typography.SmallSize;
        var titleSize = theme.Typography.SectionSize;

        var descriptionMatches =
            string.Equals(
                _descriptionFont.FontFamily.Name,
                family,
                StringComparison.Ordinal) &&
            Math.Abs(_descriptionFont.Size - descriptionSize) <= 0.01f;

        var titleMatches =
            string.Equals(
                _titleFont.FontFamily.Name,
                family,
                StringComparison.Ordinal) &&
            Math.Abs(_titleFont.Size - titleSize) <= 0.01f &&
            _titleFont.Style == FontStyle.Bold;

        if (descriptionMatches && titleMatches)
            return;

        var nextDescription = new Font(family, descriptionSize);
        var nextTitle = new Font(family, titleSize, FontStyle.Bold);

        var previousDescription = _descriptionFont;
        var previousTitle = _titleFont;

        _descriptionFont = nextDescription;
        _titleFont = nextTitle;

        foreach (var label in _titleLabels)
            label.Font = _titleFont;

        foreach (var label in _descriptionLabels)
            label.Font = _descriptionFont;

        previousDescription.Dispose();
        previousTitle.Dispose();
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
            Padding = Padding.Empty,
            AutoEllipsis = true
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

        _descriptionLabels.Add(descriptionLabel);
        _titleLabels.Add(titleLabel);

        if (!string.IsNullOrWhiteSpace(description))
            _descriptionToolTip.SetToolTip(descriptionLabel, description);

        panel.Controls.Add(descriptionLabel);
        panel.Controls.Add(titleLabel);
        return panel;
    }
}
