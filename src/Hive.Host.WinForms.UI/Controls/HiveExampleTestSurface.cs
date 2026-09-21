using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

public sealed class HiveExampleTestSurface : UserControl
{
    private readonly FlowLayoutPanel _actions;
    private readonly TableLayoutPanel _workspace;
    private readonly HiveButton _runButton;
    private readonly HiveButton _copyButton;
    private readonly Label _status;
    private readonly TextBox _input;
    private readonly TextBox _code;
    private readonly Label _inputTitle;
    private readonly Label _codeTitle;
    private readonly Label _details;
    private readonly Label _note;
    private readonly Font _sectionFont;
    private readonly Font _inputFont;
    private readonly Font _codeFont;
    private readonly Font _detailsFont;
    private readonly Font _noteFont;
    private HiveThemeDefinition? _theme;
    private CancellationTokenSource? _runCancellation;
    private bool _busy;
    private bool _compactWorkspace;
    private string _description = string.Empty;
    private string _expectedResult = string.Empty;
    private string _noteTitle = string.Empty;
    private string _noteText = string.Empty;
    private Func<CancellationToken, Task>? _runAction;
    private IHiveExampleOutput? _output;
    private IWin32Window? _owner;

    public HiveExampleTestSurface()
    {
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _sectionFont = new Font(
            "Segoe UI Semibold",
            9f,
            FontStyle.Bold);
        _inputFont = new Font("Consolas", 9f);
        _codeFont = new Font("Consolas", 9f);
        _detailsFont = new Font("Segoe UI", 9f);
        _noteFont = new Font("Segoe UI", 8.6f);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(
            new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 112));
        root.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 44));

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
            AccessibleName = "Run example",
            AccessibleDescription = "Run the current developer example.",

            Width = 126,
            Height = 36,
            Margin = Padding.Empty
        };
        _runButton.Click += async (_, _) =>
        {
            if (_runAction is null)
            {
                SetStatus("No example is configured.");
                return;
            }

            await RunAsync(
                _runAction,
                _output,
                _owner);
        };

        _copyButton = new HiveButton
        {
            Text = "Copy code",
            Style = HiveButtonStyle.Secondary,
            AccessibleName = "Copy code",
            AccessibleDescription = "Copy the C# reproduction snippet.",

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

        _workspace = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _workspace.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 50f));
        _workspace.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 50f));
        _workspace.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 28));
        _workspace.RowStyles.Add(
            new RowStyle(SizeType.Percent, 100f));

        _inputTitle = CreateSectionLabel("Test input");
        _codeTitle = CreateSectionLabel("C# reproduction snippet");

        _input = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            AccessibleName = "Test input",
            AccessibleDescription = "Editable input used by the current example.",

            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            BorderStyle = BorderStyle.FixedSingle,
            Font = _inputFont,
            Margin = new Padding(0, 0, 6, 0),
            Padding = new Padding(8)
        };

        _code = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            AccessibleName = "C# reproduction snippet",
            AccessibleDescription = "Read-only C# code showing how to reproduce the example.",

            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            BorderStyle = BorderStyle.FixedSingle,
            Font = _codeFont,
            Margin = new Padding(6, 0, 0, 0),
            Padding = new Padding(8)
        };

        _workspace.Controls.Add(_inputTitle, 0, 0);
        _workspace.Controls.Add(_codeTitle, 1, 0);
        _workspace.Controls.Add(_input, 0, 1);
        _workspace.Controls.Add(_code, 1, 1);

        _details = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Text = string.Empty,
            Font = _detailsFont,
            Padding = new Padding(1, 7, 20, 4),
            Margin = Padding.Empty,
            UseMnemonic = false,
            AutoEllipsis = false
        };

        _note = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Text = string.Empty,
            Font = _noteFont,
            Padding = new Padding(1, 4, 20, 5),
            Margin = Padding.Empty,
            UseMnemonic = false,
            AutoEllipsis = false
        };

        root.Controls.Add(_actions, 0, 0);
        root.Controls.Add(_workspace, 0, 1);
        root.Controls.Add(_details, 0, 2);
        root.Controls.Add(_note, 0, 3);
        Controls.Add(root);

        UpdateWorkspaceLayout();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateWorkspaceLayout();
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
    public string Description
    {
        get => _description;
        set
        {
            _description = value ?? string.Empty;
            UpdateDetails();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string ExpectedResult
    {
        get => _expectedResult;
        set
        {
            _expectedResult = value ?? string.Empty;
            UpdateDetails();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string NoteTitle
    {
        get => _noteTitle;
        set
        {
            _noteTitle = value ?? string.Empty;
            UpdateNote();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string NoteText
    {
        get => _noteText;
        set
        {
            _noteText = value ?? string.Empty;
            UpdateNote();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsBusy => _busy;

    public void SetInformation(
        string description,
        string expectedResult,
        string? noteTitle = null,
        string? noteText = null)
    {
        _description = description ?? string.Empty;
        _expectedResult = expectedResult ?? string.Empty;
        _noteTitle = noteTitle ?? string.Empty;
        _noteText = noteText ?? string.Empty;

        UpdateDetails();
        UpdateNote();
    }

    public void SetStatus(string text) =>
        _status.Text = text ?? string.Empty;

    public void ConfigureRun(
        Func<CancellationToken, Task> action,
        IHiveExampleOutput? output = null,
        IWin32Window? owner = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        _runAction = action;
        _output = output;
        _owner = owner;
        SetStatus("Ready");
    }

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
            _sectionFont.Dispose();
            _inputFont.Dispose();
            _codeFont.Dispose();
            _detailsFont.Dispose();
            _noteFont.Dispose();
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
        _details.ForeColor = theme.Palette.Text;
        _note.ForeColor = theme.Palette.MutedText;
        _input.BackColor = theme.Palette.InputBackground;
        _input.ForeColor = theme.Palette.Text;
        _code.BackColor = theme.Palette.InputBackground;
        _code.ForeColor = theme.Palette.Text;
    }

    private void UpdateWorkspaceLayout()
    {
        var compact = ClientSize.Width > 0 && ClientSize.Width < 820;
        if (_compactWorkspace == compact &&
            _workspace.ColumnCount == (compact ? 1 : 2))
            return;

        _compactWorkspace = compact;

        _workspace.SuspendLayout();
        try
        {
            _workspace.Controls.Clear();
            _workspace.ColumnStyles.Clear();
            _workspace.RowStyles.Clear();

            if (compact)
            {
                _workspace.ColumnCount = 1;
                _workspace.RowCount = 4;
                _workspace.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Percent, 100f));
                for (var row = 0; row < 4; row++)
                {
                    _workspace.RowStyles.Add(
                        new RowStyle(
                            row is 0 or 2
                                ? SizeType.Absolute
                                : SizeType.Percent,
                            row is 0 or 2 ? 28f : 50f));
                }

                _inputTitle.Margin = new Padding(0, 0, 0, 2);
                _codeTitle.Margin = new Padding(0, 8, 0, 2);
                _input.Margin = Padding.Empty;
                _code.Margin = Padding.Empty;

                _workspace.Controls.Add(_inputTitle, 0, 0);
                _workspace.Controls.Add(_input, 0, 1);
                _workspace.Controls.Add(_codeTitle, 0, 2);
                _workspace.Controls.Add(_code, 0, 3);
            }
            else
            {
                _workspace.ColumnCount = 2;
                _workspace.RowCount = 2;
                _workspace.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Percent, 50f));
                _workspace.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Percent, 50f));
                _workspace.RowStyles.Add(
                    new RowStyle(SizeType.Absolute, 28));
                _workspace.RowStyles.Add(
                    new RowStyle(SizeType.Percent, 100f));

                _inputTitle.Margin = Padding.Empty;
                _codeTitle.Margin = Padding.Empty;
                _input.Margin = new Padding(0, 0, 6, 0);
                _code.Margin = new Padding(6, 0, 0, 0);

                _workspace.Controls.Add(_inputTitle, 0, 0);
                _workspace.Controls.Add(_codeTitle, 1, 0);
                _workspace.Controls.Add(_input, 0, 1);
                _workspace.Controls.Add(_code, 1, 1);
            }
        }
        finally
        {
            _workspace.ResumeLayout(true);
        }
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

    private void UpdateDetails()
    {
        var expected = string.IsNullOrWhiteSpace(_expectedResult)
            ? string.Empty
            : Environment.NewLine + Environment.NewLine +
              "Expected result" + Environment.NewLine +
              _expectedResult;

        _details.Text =
            (string.IsNullOrWhiteSpace(_description)
                ? string.Empty
                : "Description" + Environment.NewLine + _description) +
            expected;
    }

    private void UpdateNote()
    {
        _note.Text = string.IsNullOrWhiteSpace(_noteTitle)
            ? _noteText
            : string.IsNullOrWhiteSpace(_noteText)
                ? _noteTitle
                : _noteTitle + ": " + _noteText;
    }

    private IHiveThemeManager? FindHiveThemeManager()
    {
        if (FindForm() is HiveForm form)
            return form.ThemeManager;

        return null;
    }

    private Label CreateSectionLabel(string text) =>
        new()
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Font = _sectionFont,
            Margin = Padding.Empty,
            Padding = new Padding(2, 2, 0, 0),
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft
        };
}
