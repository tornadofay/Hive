using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;
using System.ComponentModel;

namespace Hive.Host.WinForms.UI.Controls;

internal sealed class HiveWindowHeader : Control
{
    private const int HeaderHeight = 54;
    private const int ButtonWidth = 42;
    private const int WindowCommandNone = 0;
    private const int WindowCommandClose = 1;
    private const int WindowCommandMinimize = 2;
    private const int WindowCommandHelp = 3;
    private const int WindowCommandTheme = 4;
    private const int WindowCommandMaximize = 5;

    private string _title = string.Empty;
    private string _subtitle = string.Empty;
    private int _hoveredCommand;
    private int _pressedCommand;
    private bool _allowMove = true;
    private bool _allowClose = true;
    private bool _allowMinimize;
    private bool _allowHelp;
    private bool _allowThemeToggle = true;
    private bool _allowMaximize = true;

    private Color _background1;
    private Color _foreground;
    private Color _subtitleForeground;
    private Color _buttonHover;
    private Color _buttonPressed;
    private Color _closeHover;
    private Pen? _borderPen;
    private SolidBrush? _backgroundBrush;
    private SolidBrush? _buttonHoverBrush;
    private SolidBrush? _buttonPressedBrush;
    private SolidBrush? _closeHoverBrush;

    private readonly Font _titleFont = new("Segoe UI Semibold", 10.5f, FontStyle.Bold);
    private readonly Font _subtitleFont = new("Segoe UI", 8.25f, FontStyle.Regular);
    private readonly Font _buttonFont = new("Segoe UI Symbol", 11.5f, FontStyle.Regular);

    public HiveWindowHeader()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);

        Dock = DockStyle.Top;
        Height = HeaderHeight;
        MinimumSize = new Size(0, HeaderHeight);
        Cursor = Cursors.Default;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public string Title
    {
        get => _title;
        set
        {
            var next = value ?? string.Empty;
            if (string.Equals(_title, next, StringComparison.Ordinal))
                return;

            _title = next;
            Text = next;
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public string Subtitle
    {
        get => _subtitle;
        set
        {
            var next = value ?? string.Empty;
            if (string.Equals(_subtitle, next, StringComparison.Ordinal))
                return;

            _subtitle = next;
            Invalidate();
        }
    }


    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public bool AllowMove
    {
        get => _allowMove;
        set => _allowMove = value;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public bool AllowClose
    {
        get => _allowClose;
        set
        {
            if (_allowClose == value)
                return;

            _allowClose = value;
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public bool AllowMinimize
    {
        get => _allowMinimize;
        set
        {
            if (_allowMinimize == value)
                return;

            _allowMinimize = value;
            Invalidate();
        }
    }


    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public bool AllowThemeToggle
    {
        get => _allowThemeToggle;
        set
        {
            if (_allowThemeToggle == value)
                return;

            _allowThemeToggle = value;
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public bool AllowMaximize
    {
        get => _allowMaximize;
        set
        {
            if (_allowMaximize == value)
                return;

            _allowMaximize = value;
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public bool AllowHelp
    {
        get => _allowHelp;
        set
        {
            if (_allowHelp == value)
                return;

            _allowHelp = value;
            Invalidate();
        }
    }

    public event EventHandler? HelpClicked;

    public void ApplyTheme(HiveThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        _background1 = theme.Palette.ElevatedSurface;
        _foreground = theme.Palette.Text;
        _subtitleForeground = theme.Palette.MutedText;
        _buttonHover = theme.VisualStates.HoverBackground;
        _buttonPressed = theme.VisualStates.PressedBackground;
        _closeHover = theme.VisualStates.Error;

        _borderPen?.Dispose();
        _borderPen = new Pen(theme.Palette.Border);

        _backgroundBrush?.Dispose();
        _backgroundBrush = new SolidBrush(_background1);
        _buttonHoverBrush?.Dispose();
        _buttonHoverBrush = new SolidBrush(_buttonHover);
        _buttonPressedBrush?.Dispose();
        _buttonPressedBrush = new SolidBrush(_buttonPressed);
        _closeHoverBrush?.Dispose();
        _closeHoverBrush = new SolidBrush(_closeHover);

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var bounds = ClientRectangle;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        if (_backgroundBrush is not null)
            e.Graphics.FillRectangle(_backgroundBrush, bounds);

        if (_borderPen is not null)
            e.Graphics.DrawLine(_borderPen, 0, Height - 1, Width - 1, Height - 1);

        var textLeft = 20;
        var textWidth = Math.Max(
            80,
            Width - textLeft - GetButtonCount() * ButtonWidth - 18);

        if (string.IsNullOrWhiteSpace(_subtitle))
        {
            TextRenderer.DrawText(
                e.Graphics,
                _title,
                _titleFont,
                new Rectangle(textLeft, 0, textWidth, Height),
                _foreground,
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);
        }
        else
        {
            TextRenderer.DrawText(
                e.Graphics,
                _title,
                _titleFont,
                new Rectangle(textLeft, 5, textWidth, 24),
                _foreground,
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);

            TextRenderer.DrawText(
                e.Graphics,
                _subtitle,
                _subtitleFont,
                new Rectangle(textLeft, 28, textWidth, 20),
                _subtitleForeground,
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);
        }

        var form = FindForm();
        var themeGlyph = form is HiveForm hiveForm &&
                         hiveForm.Theme.Palette.WindowBackground.GetBrightness() < 0.5f
            ? "☀"
            : "☾";

        var maximizeGlyph = form?.WindowState == FormWindowState.Maximized
            ? "❐"
            : "□";

        DrawCommandButton(e.Graphics, WindowCommandHelp, _allowHelp, "?");
        DrawCommandButton(e.Graphics, WindowCommandTheme, _allowThemeToggle, themeGlyph);
        DrawCommandButton(e.Graphics, WindowCommandMinimize, _allowMinimize, "—");
        DrawCommandButton(e.Graphics, WindowCommandMaximize, _allowMaximize, maximizeGlyph);
        DrawCommandButton(e.Graphics, WindowCommandClose, _allowClose, "×");
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var command = HitTestCommand(e.Location);
        if (_hoveredCommand == command)
            return;

        var previousCommand = _hoveredCommand;
        _hoveredCommand = command;
        Cursor = command == WindowCommandNone
            ? Cursors.Default
            : Cursors.Hand;

        if (previousCommand != WindowCommandNone)
            Invalidate(GetCommandBounds(previousCommand));

        if (command != WindowCommandNone)
            Invalidate(GetCommandBounds(command));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        var previousCommand = _hoveredCommand;
        _hoveredCommand = WindowCommandNone;
        _pressedCommand = WindowCommandNone;
        Cursor = Cursors.Default;

        if (previousCommand != WindowCommandNone)
            Invalidate(GetCommandBounds(previousCommand));
    }

    protected override void OnDoubleClick(EventArgs e)
    {
        base.OnDoubleClick(e);

        if (!_allowMaximize ||
            HitTestCommand(PointToClient(Cursor.Position)) != WindowCommandNone)
            return;

        if (FindForm() is { } form &&
            form.FormBorderStyle == FormBorderStyle.None)
        {
            form.WindowState =
                form.WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal
                    : FormWindowState.Maximized;
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left)
            return;

        var command = HitTestCommand(e.Location);
        if (command != WindowCommandNone)
        {
            _pressedCommand = command;
            Invalidate(GetCommandBounds(command));
            return;
        }

        if (_allowMove)
            BeginWindowMove();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button != MouseButtons.Left)
            return;

        var command = _pressedCommand;
        _pressedCommand = WindowCommandNone;

        if (command != WindowCommandNone)
            Invalidate(GetCommandBounds(command));

        if (command == WindowCommandNone || command != HitTestCommand(e.Location))
            return;

        switch (command)
        {
            case WindowCommandClose:
                FindForm()?.Close();
                break;

            case WindowCommandMinimize:
                if (FindForm() is { } form)
                    form.WindowState = FormWindowState.Minimized;
                break;

            case WindowCommandMaximize:
                if (FindForm() is { } maximizeForm)
                {
                    maximizeForm.WindowState =
                        maximizeForm.WindowState == FormWindowState.Maximized
                            ? FormWindowState.Normal
                            : FormWindowState.Maximized;
                }
                break;

            case WindowCommandTheme:
                if (FindForm() is HiveForm hiveForm)
                {
                    var isDark =
                        hiveForm.Theme.Palette.WindowBackground.GetBrightness() < 0.5f;

                    hiveForm.ThemeManager.SetMode(
                        isDark
                            ? HiveThemeMode.Light
                            : HiveThemeMode.Dark);
                }
                break;

            case WindowCommandHelp:
                HelpClicked?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);

        if (!Capture && _pressedCommand != WindowCommandNone)
        {
            var command = _pressedCommand;
            _pressedCommand = WindowCommandNone;
            Invalidate(GetCommandBounds(command));
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _titleFont.Dispose();
            _subtitleFont.Dispose();
            _buttonFont.Dispose();
            _borderPen?.Dispose();
            _backgroundBrush?.Dispose();
            _buttonHoverBrush?.Dispose();
            _buttonPressedBrush?.Dispose();
            _closeHoverBrush?.Dispose();
        }
    }

    private int GetButtonCount() =>
        (_allowHelp ? 1 : 0) +
        (_allowThemeToggle ? 1 : 0) +
        (_allowMinimize ? 1 : 0) +
        (_allowMaximize ? 1 : 0) +
        (_allowClose ? 1 : 0);

    private void DrawCommandButton(
        Graphics graphics,
        int command,
        bool visible,
        string glyph)
    {
        if (!visible)
            return;

        var rect = GetCommandBounds(command);
        var hovered = _hoveredCommand == command;
        var pressed = _pressedCommand == command;

        if (hovered || pressed)
        {
            var brush = command == WindowCommandClose
                ? _closeHoverBrush
                : pressed
                    ? _buttonPressedBrush
                    : _buttonHoverBrush;

            if (brush is not null)
                graphics.FillRectangle(brush, rect);
        }

        TextRenderer.DrawText(
            graphics,
            glyph,
            _buttonFont,
            rect,
            _foreground,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPadding);
    }

    private int HitTestCommand(Point point)
    {
        if (point.Y < 0 || point.Y >= Height)
            return WindowCommandNone;

        if (_allowClose && GetCommandBounds(WindowCommandClose).Contains(point))
            return WindowCommandClose;

        if (_allowMaximize && GetCommandBounds(WindowCommandMaximize).Contains(point))
            return WindowCommandMaximize;

        if (_allowMinimize && GetCommandBounds(WindowCommandMinimize).Contains(point))
            return WindowCommandMinimize;

        if (_allowThemeToggle && GetCommandBounds(WindowCommandTheme).Contains(point))
            return WindowCommandTheme;

        if (_allowHelp && GetCommandBounds(WindowCommandHelp).Contains(point))
            return WindowCommandHelp;

        return WindowCommandNone;
    }

    private Rectangle GetCommandBounds(int command)
    {
        var indexFromRight = command switch
        {
            WindowCommandClose when _allowClose => 0,
            WindowCommandMaximize when _allowMaximize =>
                (_allowClose ? 1 : 0),
            WindowCommandMinimize when _allowMinimize =>
                (_allowClose ? 1 : 0) + (_allowMaximize ? 1 : 0),
            WindowCommandTheme when _allowThemeToggle =>
                (_allowClose ? 1 : 0) +
                (_allowMaximize ? 1 : 0) +
                (_allowMinimize ? 1 : 0),
            WindowCommandHelp when _allowHelp =>
                (_allowClose ? 1 : 0) +
                (_allowMaximize ? 1 : 0) +
                (_allowMinimize ? 1 : 0) +
                (_allowThemeToggle ? 1 : 0),
            _ => -1
        };

        return indexFromRight < 0
            ? Rectangle.Empty
            : new Rectangle(
                Width - ButtonWidth * (indexFromRight + 1),
                0,
                ButtonWidth,
                Height);
    }

    private void BeginWindowMove()
    {
        var form = FindForm();
        if (form is null)
            return;

        ReleaseCapture();
        SendMessage(form.Handle, 0x00A1, new IntPtr(2), IntPtr.Zero);
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(
        IntPtr hWnd,
        int message,
        IntPtr wParam,
        IntPtr lParam);
}
