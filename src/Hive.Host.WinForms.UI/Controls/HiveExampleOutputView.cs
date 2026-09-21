using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

public sealed class HiveExampleOutputView : UserControl, IHiveExampleOutput
{
    private readonly TableLayoutPanel _root;
    private readonly TableLayoutPanel _header;
    private readonly TableLayoutPanel _titleLayout;
    private readonly Label _title;
    private readonly Label _meta;
    private readonly Button _copyButton;
    private readonly Button _clearButton;
    private readonly Button _toggleButton;
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
        _metaFont = new Font("Segoe UI", 8.1f);
        _outputFont = new Font("Consolas", 9f);

        _root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = new Padding(10, 4, 10, 4),
        };
        _root.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100f));
        _root.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 40));
        _root.RowStyles.Add(
            new RowStyle(SizeType.Percent, 100f));

        _header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        _header.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100f));
        _header.ColumnStyles.Add(
            new ColumnStyle(SizeType.Absolute, 92));
        _header.ColumnStyles.Add(
            new ColumnStyle(SizeType.Absolute, 92));
        _header.ColumnStyles.Add(
            new ColumnStyle(SizeType.Absolute, 92));

        _titleLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = new Padding(2, 1, 8, 1)
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

        _copyButton = CreateActionButton("Copy", enabled: false);
        _copyButton.Margin = new Padding(6, 0, 0, 0);
        _copyButton.Click += (_, _) => Copy();

        _clearButton = CreateActionButton("Clear");
        _clearButton.Margin = new Padding(6, 0, 0, 0);
        _clearButton.Click += (_, _) => Clear();

        _toggleButton = CreateActionButton("Hide");
        _toggleButton.Margin = new Padding(6, 0, 0, 0);
        _toggleButton.Click += (_, _) => ToggleCollapsed();

        _outputFrame = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 0),
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
            Padding = new Padding(8),
        };

        _outputFrame.Controls.Add(_output);

        _header.Controls.Add(_titleLayout, 0, 0);
        _header.Controls.Add(_copyButton, 1, 0);
        _header.Controls.Add(_clearButton, 2, 0);
        _header.Controls.Add(_toggleButton, 3, 0);
        _root.Controls.Add(_header, 0, 0);
        _root.Controls.Add(_outputFrame, 0, 1);

        Controls.Add(_root);

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
        _toggleButton.Text = "Hide";
        CollapseStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
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
        _output.AppendText(value ?? string.Empty);
        _output.SelectionStart = _output.TextLength;
        _output.ScrollToCaret();
        UpdateActionState();
        OutputAvailabilityChanged?.Invoke(this, EventArgs.Empty);
    }

    private static Button CreateActionButton(
        string text,
        bool enabled = true) =>
        new()
        {
            AutoSize = false,
            Text = text,
            Dock = DockStyle.Fill,
            Height = 32,
            Margin = Padding.Empty,
            Padding = new Padding(8, 0, 8, 0),
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
            TabStop = true,
            Enabled = enabled
        };

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
        _title.ForeColor = theme.Palette.Text;
        ApplyActionButtonTheme(_copyButton, theme);
        ApplyActionButtonTheme(_clearButton, theme);
        ApplyActionButtonTheme(_toggleButton, theme);
        _output.BackColor = theme.Palette.InputBackground;
        _output.ForeColor = theme.Palette.Text;
        _outputFrame.BackColor = theme.Palette.Border;
        _meta.ForeColor = theme.Palette.MutedText;
    }

    private static void ApplyActionButtonTheme(
        Button button,
        HiveThemeDefinition theme)
    {
        button.BackColor = button.Enabled
            ? theme.Palette.ElevatedSurface
            : theme.Palette.DisabledBackground;
        button.ForeColor = button.Enabled
            ? theme.Palette.Text
            : theme.Palette.DisabledText;
        button.FlatAppearance.BorderColor = button.Enabled
            ? theme.Palette.Border
            : theme.VisualStates.DisabledBorder;
        button.FlatAppearance.BorderSize = 1;
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
