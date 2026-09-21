using System.Drawing;
using System.Drawing.Drawing2D;
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

    private string _title = string.Empty;
    private string _subtitle = string.Empty;
    private int _hoveredCommand;
    private int _pressedCommand;
    private bool _allowMove = true;
    private bool _allowClose = true;
    private bool _allowMinimize;
    private bool _allowHelp;

    private Color _background1;
    private Color _background2;
    private Color _foreground;
    private Color _subtitleForeground;
    private Color _buttonHover;
    private Color _buttonPressed;
    private Color _closeHover;

    private readonly Font _titleFont = new("Segoe UI", 10f, FontStyle.Bold);
    private readonly Font _subtitleFont = new("Segoe UI", 8f, FontStyle.Regular);
    private readonly Font _buttonFont = new("Segoe UI Symbol", 12f, FontStyle.Regular);

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

        _background1 = theme.VisualStates.NavigationBackground;
        _background2 = theme.Mode == HiveThemeMode.Dark
            ? ColorTranslator.FromHtml("#254C68")
            : ColorTranslator.FromHtml("#2A5F86");
        _foreground = theme.VisualStates.NavigationText;
        _subtitleForeground = Color.FromArgb(210, _foreground);
        _buttonHover = Color.FromArgb(54, Color.White);
        _buttonPressed = Color.FromArgb(82, Color.White);
        _closeHover = Color.FromArgb(204, 72, 72);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var bounds = ClientRectangle;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var background = new LinearGradientBrush(
            bounds,
            _background1,
            _background2,
            90f);

        e.Graphics.FillRectangle(background, bounds);

        var textLeft = 18;
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

        DrawCommandButton(e.Graphics, WindowCommandHelp, _allowHelp, "?");
        DrawCommandButton(e.Graphics, WindowCommandMinimize, _allowMinimize, "—");
        DrawCommandButton(e.Graphics, WindowCommandClose, _allowClose, "×");
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var command = HitTestCommand(e.Location);
        if (_hoveredCommand == command)
            return;

        _hoveredCommand = command;
        Cursor = command == WindowCommandNone
            ? Cursors.Default
            : Cursors.Hand;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoveredCommand = WindowCommandNone;
        _pressedCommand = WindowCommandNone;
        Cursor = Cursors.Default;
        Invalidate();
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
            Invalidate();
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
        Invalidate();

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

            case WindowCommandHelp:
                HelpClicked?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont.Dispose();
            _subtitleFont.Dispose();
            _buttonFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private int GetButtonCount() =>
        (_allowHelp ? 1 : 0) +
        (_allowMinimize ? 1 : 0) +
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
            var fill = command == WindowCommandClose
                ? _closeHover
                : pressed
                    ? _buttonPressed
                    : _buttonHover;

            using var brush = new SolidBrush(fill);
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

        if (_allowMinimize && GetCommandBounds(WindowCommandMinimize).Contains(point))
            return WindowCommandMinimize;

        if (_allowHelp && GetCommandBounds(WindowCommandHelp).Contains(point))
            return WindowCommandHelp;

        return WindowCommandNone;
    }

    private Rectangle GetCommandBounds(int command)
    {
        var indexFromRight = command switch
        {
            WindowCommandClose when _allowClose => 0,
            WindowCommandMinimize when _allowMinimize => _allowClose ? 1 : 0,
            WindowCommandHelp when _allowHelp =>
                (_allowClose ? 1 : 0) + (_allowMinimize ? 1 : 0),
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
