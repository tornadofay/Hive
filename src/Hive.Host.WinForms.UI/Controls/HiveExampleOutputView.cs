using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

public sealed class HiveExampleOutputView : UserControl, IHiveExampleOutput
{
    private readonly Label _title;
    private readonly HiveButton _clearButton;
    private readonly TextBox _output;
    private readonly Font _titleFont;
    private readonly Font _outputFont;

    public HiveExampleOutputView()
    {
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _titleFont = new Font(
            "Segoe UI Semibold",
            9.5f,
            FontStyle.Bold);
        _outputFont = new Font("Consolas", 9f);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = new Padding(10),
        };
        root.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 40));
        root.RowStyles.Add(
            new RowStyle(SizeType.Percent, 100f));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        header.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100f));
        header.ColumnStyles.Add(
            new ColumnStyle(SizeType.Absolute, 96));

        _title = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Font = _titleFont,
            Margin = Padding.Empty,
            Padding = new Padding(2, 0, 0, 0),
            Text = "Example output",
            TextAlign = ContentAlignment.MiddleLeft
        };

        _clearButton = new HiveButton
        {
            Text = "Clear",
            Style = HiveButtonStyle.Secondary,
            Dock = DockStyle.Fill,
            MinimumSize = new Size(88, 32),
            Size = new Size(88, 32),
            Margin = Padding.Empty
        };
        _clearButton.Click += (_, _) => Clear();

        _output = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            BorderStyle = BorderStyle.FixedSingle,
            Font = _outputFont,
            Margin = Padding.Empty,
            Padding = new Padding(8),
        };

        header.Controls.Add(_title, 0, 0);
        header.Controls.Add(_clearButton, 1, 0);
        root.Controls.Add(header, 0, 0);
        root.Controls.Add(_output, 0, 1);

        Controls.Add(root);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Text
    {
        get => _output.Text;
        set => _output.Text = value ?? string.Empty;
    }

    public TextBox OutputTextBox => _output;

    public void Clear() => _output.Clear();

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
    }

    public void Append(string value)
    {
        _output.AppendText(value ?? string.Empty);
        _output.SelectionStart = _output.TextLength;
        _output.ScrollToCaret();
    }

    internal void ApplyTheme(HiveThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        _theme = theme;
        BackColor = theme.Palette.ElevatedSurface;
        _title.ForeColor = theme.Palette.Text;
        _output.BackColor = theme.Palette.InputBackground;
        _output.ForeColor = theme.Palette.Text;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont.Dispose();
            _outputFont.Dispose();
        }

        base.Dispose(disposing);
    }
}
