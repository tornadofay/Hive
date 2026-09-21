using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

public sealed class HiveExampleTestSurface : UserControl
{
    private readonly FlowLayoutPanel _actions;
    private readonly HiveButton _runButton;
    private readonly HiveButton _copyButton;
    private readonly Label _status;
    private readonly TextBox _input;
    private readonly TextBox _code;
    private readonly Label _inputTitle;
    private readonly Label _codeTitle;
    private readonly Font _sectionFont;
    private HiveThemeDefinition? _theme;
    private CancellationTokenSource? _runCancellation;
    private bool _busy;

    public HiveExampleTestSurface()
    {
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _sectionFont = new Font(
            "Segoe UI Semibold",
            9f,
            FontStyle.Bold);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(
            new RowStyle(SizeType.Percent, 100f));

        _actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = new Padding(0, 4, 0, 4)
        };

        _runButton = new HiveButton
        {
            Text = "Run example",
            Style = HiveButtonStyle.Primary,
            Width = 126,
            Height = 36,
            Margin = Padding.Empty
        };

        _copyButton = new HiveButton
        {
            Text = "Copy code",
            Style = HiveButtonStyle.Secondary,
            Width = 106,
            Height = 36,
            Margin = new Padding(8, 0, 0, 0)
        };
        _copyButton.Click += (_, _) => CopyCode();

        _status = new Label
        {
            AutoSize = true,
            Height = 36,
            Text = "Ready",
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(12, 0, 0, 0),
            Padding = Padding.Empty
        };

        _actions.Controls.Add(_runButton);
        _actions.Controls.Add(_copyButton);
        _actions.Controls.Add(_status);

        var workspace = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        workspace.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 50f));
        workspace.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 50f));
        workspace.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 28));
        workspace.RowStyles.Add(
            new RowStyle(SizeType.Percent, 100f));

        _inputTitle = CreateSectionLabel("Test input");
        _codeTitle = CreateSectionLabel("C# reproduction snippet");

        _input = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 9f),
            Margin = new Padding(0, 0, 6, 0),
            Padding = new Padding(8)
        };

        _code = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 9f),
            Margin = new Padding(6, 0, 0, 0),
            Padding = new Padding(8)
        };

        workspace.Controls.Add(_inputTitle, 0, 0);
        workspace.Controls.Add(_codeTitle, 1, 0);
        workspace.Controls.Add(_input, 0, 1);
        workspace.Controls.Add(_code, 1, 1);

        root.Controls.Add(_actions, 0, 0);
        root.Controls.Add(workspace, 0, 1);
        Controls.Add(root);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public HiveButton RunButton => _runButton;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public HiveButton CopyCodeButton => _copyButton;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Label StatusLabel => _status;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextBox InputTextBox => _input;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextBox CodeTextBox => _code;

    public FlowLayoutPanel ActionPanel => _actions;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string InputText
    {
        get => _input.Text;
        set => _input.Text = value ?? string.Empty;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string CodeSnippet
    {
        get => _code.Text;
        set => _code.Text = value ?? string.Empty;
    }

    [DefaultValue("Run example")]
    public string RunButtonText
    {
        get => _runButton.Text;
        set => _runButton.Text = value ?? string.Empty;
    }

    [DefaultValue("Test input")]
    public string InputTitle
    {
        get => _inputTitle.Text;
        set => _inputTitle.Text = value ?? string.Empty;
    }

    [DefaultValue("C# reproduction snippet")]
    public string CodeTitle
    {
        get => _codeTitle.Text;
        set => _codeTitle.Text = value ?? string.Empty;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsBusy => _busy;

    public void SetStatus(string text) =>
        _status.Text = text ?? string.Empty;

    public void Cancel()
    {
        _runCancellation?.Cancel();
    }

    public async Task RunAsync(
        Func<CancellationToken, Task> action,
        IHiveExampleOutput? output = null,
        IWin32Window? owner = null,
        string busyText = "Running example...")
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_busy)
            return;

        _runCancellation?.Dispose();
        _runCancellation = new CancellationTokenSource();
        _busy = true;
        SetBusy(true, busyText);

        try
        {
            await action(_runCancellation.Token).ConfigureAwait(true);
            SetStatus("Completed.");
        }
        catch (OperationCanceledException) when (_runCancellation.IsCancellationRequested)
        {
            SetStatus("Cancelled.");
        }
        catch (Exception exception)
        {
            output?.Write(
                "EXCEPTION",
                exception.ToString());

            SetStatus("Failed.");

            var window = owner ?? FindForm();
            if (window is not null)
            {
                HiveMessageBox.Show(
                    window,
                    new HiveMessageOptions(
                        "Example failed",
                        "The example could not be completed.",
                        HiveMessageType.Error,
                        MessageBoxButtons.OK,
                        exception.ToString(),
                        DetailsExpanded: true),
                    _theme is null
                        ? null
                        : FindHiveThemeManager());
            }
        }
        finally
        {
            _busy = false;
            SetBusy(false, _status.Text);
            _runCancellation?.Dispose();
            _runCancellation = null;
        }
    }

    public static string RequireInput(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(
                "The example input cannot be empty.",
                nameof(value));

        return value;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _runCancellation?.Cancel();
            _runCancellation?.Dispose();
        }

        base.Dispose(disposing);
    }

    internal void ApplyTheme(HiveThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        _theme = theme;
        BackColor = theme.Palette.Surface;
        _status.ForeColor = theme.Palette.MutedText;
        _inputTitle.ForeColor = theme.Palette.Text;
        _codeTitle.ForeColor = theme.Palette.Text;
        _input.BackColor = theme.Palette.InputBackground;
        _input.ForeColor = theme.Palette.Text;
        _code.BackColor = theme.Palette.InputBackground;
        _code.ForeColor = theme.Palette.Text;
    }

    private void SetBusy(
        bool busy,
        string text)
    {
        _runButton.Enabled = !busy;
        _copyButton.Enabled = !busy;
        _input.Enabled = !busy;
        _status.Text = text;
    }

    private void CopyCode()
    {
        if (string.IsNullOrEmpty(_code.Text))
        {
            SetStatus("No code to copy.");
            return;
        }

        try
        {
            Clipboard.SetText(_code.Text);
            SetStatus("Code copied.");
        }
        catch (ExternalException exception)
        {
            SetStatus("Copy failed.");
            HiveMessageBox.Show(
                FindForm(),
                new HiveMessageOptions(
                    "Copy failed",
                    "The code snippet could not be copied to the clipboard.",
                    HiveMessageType.Error,
                    MessageBoxButtons.OK,
                    exception.ToString(),
                    DetailsExpanded: true),
                FindHiveThemeManager());
        }
    }

    private IHiveThemeManager? FindHiveThemeManager()
    {
        if (FindForm() is HiveForm form)
            return form.ThemeManager;

        return null;
    }

    private static Label CreateSectionLabel(string text) =>
        new()
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Font = new Font(
                "Segoe UI Semibold",
                9f,
                FontStyle.Bold),
            Margin = Padding.Empty,
            Padding = new Padding(2, 2, 0, 0),
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft
        };
    
    }
