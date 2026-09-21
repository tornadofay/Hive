using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

public sealed class HiveExampleOutputView : UserControl, IHiveExampleOutput
{
    private const int HeaderHeight = 48;
    private const int ActionWidth = 88;
    private const int ActionGap = 8;
    private const int SurfaceRadius = 8;

    private readonly HiveBorderPanel _surface;
    private readonly TableLayoutPanel _root;
    private readonly TableLayoutPanel _header;
    private readonly TableLayoutPanel _titleLayout;
    private readonly FlowLayoutPanel _actions;
    private readonly Label _title;
    private readonly Label _meta;
    private readonly HiveButton _copyButton;
    private readonly HiveButton _clearButton;
    private readonly HiveButton _toggleButton;
    private readonly Panel _outputFrame;
    private readonly TextBox _output;
    private readonly Font _titleFont;
    private readonly Font _metaFont;
    private readonly Font _outputFont;
    private bool _collapsed = true;

    public HiveExampleOutputView()
    {
        Dock = DockStyle.None;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _titleFont = new Font(
            "Segoe UI Semibold",
            9.5f,
            FontStyle.Bold);
        _metaFont = new Font("Segoe UI", 8.2f);
        _outputFont = new Font("Consolas", 9f);

        _surface = new HiveBorderPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(1),
            CornerRadius = SurfaceRadius
        };

        _root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        _root.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100f));
        _root.RowStyles.Add(
            new RowStyle(SizeType.Absolute, HeaderHeight));
        _root.RowStyles.Add(
            new RowStyle(SizeType.Percent, 100f));

        _header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = new Padding(10, 7, 10, 7),
        };
        _header.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100f));
        _header.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Absolute,
                (ActionWidth * 3) + (ActionGap * 2)));

        _titleLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = new Padding(2, 0, 12, 0)
        };
        _titleLayout.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100f));
        _titleLayout.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 20));
        _titleLayout.RowStyles.Add(
            new RowStyle(SizeType.Percent, 100f));

        _title = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Font = _titleFont,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Text = "Output",
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };

        _meta = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Font = _metaFont,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Text = "No output yet",
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };

        _titleLayout.Controls.Add(_title, 0, 0);
        _titleLayout.Controls.Add(_meta, 0, 1);

        _actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        _copyButton = CreateActionButton("Copy");
        _clearButton = CreateActionButton("Clear");
        _toggleButton = CreateActionButton("Hide");

        _copyButton.Click += (_, _) => Copy();
        _clearButton.Click += (_, _) => Clear();
        _toggleButton.Click += (_, _) => ToggleCollapsed();

        _actions.Controls.Add(_copyButton);
        _actions.Controls.Add(_clearButton);
        _actions.Controls.Add(_toggleButton);

        _outputFrame = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(10, 0, 10, 10),
            Padding = new Padding(1)
        };

        _output = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            BorderStyle = BorderStyle.None,
            Font = _outputFont,
            Margin = Padding.Empty,
            Padding = new Padding(8, 6, 8, 6),
        };

        _outputFrame.Controls.Add(_output);

        _header.Controls.Add(_titleLayout, 0, 0);
        _header.Controls.Add(_actions, 1, 0);

        _root.Controls.Add(_header, 0, 0);
        _root.Controls.Add(_outputFrame, 0, 1);
        _surface.Controls.Add(_root);
        Controls.Add(_surface);

        _outputFrame.Visible = false;
        Visible = false;
        UpdateActionState();
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string OutputText
    {
        get => _output.Text;
        set
        {
            _output.Text = value ?? string.Empty;
            UpdateActionState();

            if (_output.TextLength > 0)
                OutputAvailabilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public TextBox OutputTextBox => _output;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsCollapsed => _collapsed;

    public event EventHandler? CollapseStateChanged;

    public event EventHandler? OutputAvailabilityChanged;

    public void ToggleCollapsed() =>
        SetCollapsed(!_collapsed);

    public void SetCollapsed(bool collapsed)
    {
        if (_collapsed == collapsed)
            return;

        _collapsed = collapsed;
        Visible = !collapsed;
        _outputFrame.Visible = !collapsed;

        // Keep the action control visually deterministic. The control itself
        // remains the same HiveButton in both states.
        _toggleButton.Text = collapsed ? "Show" : "Hide";

        CollapseStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        if (_output.TextLength == 0)
            return;

        _output.Clear();
        UpdateActionState();
    }

    public void Write(
        string title,
        string value)
    {
        var header = string.IsNullOrWhiteSpace(title)
            ? string.Empty
            : $"[{title}]{Environment.NewLine}";

        _output.Text =
            header +
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
            Environment.NewLine +
            (value ?? string.Empty);

        _output.SelectionStart = _output.TextLength;
        _output.ScrollToCaret();
        UpdateActionState();
        OutputAvailabilityChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Append(string value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        _output.AppendText(value);
        _output.SelectionStart = _output.TextLength;
        _output.ScrollToCaret();
        UpdateActionState();
        OutputAvailabilityChanged?.Invoke(this, EventArgs.Empty);
    }

    private static HiveButton CreateActionButton(string text)
    {
        return new HiveButton
        {
            Text = text,
            Style = HiveButtonStyle.Secondary,
            Width = ActionWidth,
            Height = 34,
            Margin = new Padding(ActionGap / 2, 0, ActionGap / 2, 0),
            AccessibleRole = AccessibleRole.PushButton,
            TabStop = true
        };
    }

    private void Copy()
    {
        if (string.IsNullOrEmpty(_output.Text))
            return;

        try
        {
            Clipboard.SetText(_output.Text);
        }
        catch (ExternalException exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"HiveExampleOutputView clipboard copy failed: {exception}");
        }
    }

    private void UpdateActionState()
    {
        var hasOutput = _output.TextLength > 0;
        _copyButton.Enabled = hasOutput;

        if (!hasOutput)
        {
            _meta.Text = "No output yet";
            _clearButton.Enabled = false;
            return;
        }

        var lineCount = 1;
        for (var index = 0; index < _output.TextLength; index++)
        {
            if (_output.Text[index] == '\n')
                lineCount++;
        }

        _meta.Text = lineCount == 1
            ? "1 line"
            : $"{lineCount:N0} lines";

        _clearButton.Enabled = true;
    }

    internal void ApplyTheme(HiveThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        BackColor = theme.Palette.ElevatedSurface;
        _surface.BackColor = theme.Palette.ElevatedSurface;
        _title.ForeColor = theme.Palette.Text;
        _meta.ForeColor = theme.Palette.MutedText;
        _outputFrame.BackColor = theme.Palette.Border;
        _output.BackColor = theme.Palette.InputBackground;
        _output.ForeColor = theme.Palette.Text;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont.Dispose();
            _metaFont.Dispose();
            _outputFont.Dispose();
        }

        base.Dispose(disposing);
    }
}
