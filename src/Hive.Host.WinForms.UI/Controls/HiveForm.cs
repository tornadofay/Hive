using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

public abstract class HiveForm : Form
{
    private const int CornerRadius = 10;
    private const int ResizeGripSize = 8;
    private const int WmNcHitTest = 0x0084;
    private const int HtClient = 1;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;

    private readonly HiveWindowHeader _header;
    private readonly Panel _bodyPanel;
    private IHiveThemeManager _themeManager;
    private Font _formFont;
    private Padding _bodyPadding;
    private Region? _windowRegion;

    protected HiveForm(
        string title,
        string subtitle = "",
        Size? initialSize = null,
        Size? minimumSize = null,
        IHiveThemeManager? themeManager = null)
    {
        _themeManager = themeManager ?? new HiveThemeManager();

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        Padding = new Padding(2);
        MinimumSize = minimumSize ?? new Size(640, 420);
        Size = initialSize ?? new Size(900, 600);
        _formFont = new Font(
            _themeManager.Theme.Typography.FontFamily,
            _themeManager.Theme.Typography.BodySize);
        Font = _formFont;

        _header = new HiveWindowHeader
        {
            Title = title,
            Subtitle = subtitle,
            AllowMove = true,
            AllowClose = true,
            AllowMinimize = false,
            AllowHelp = false
        };

        _bodyPadding = new Padding(_themeManager.Theme.Spacing.Xl);
        _bodyPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = _bodyPadding,
            BackColor = _themeManager.Theme.Palette.Surface
        };

        Controls.Add(_bodyPanel);
        Controls.Add(_header);

        _themeManager.ThemeChanged += ThemeManagerOnThemeChanged;
        _header.HelpClicked += HeaderOnHelpClicked;
        Shown += (_, _) => UpdateWindowRegion();

        ApplyTheme();
    }

    public IHiveThemeManager ThemeManager => _themeManager;

    public HiveThemeDefinition Theme => _themeManager.Theme;

    protected Panel BodyPanel => _bodyPanel;

    
    protected void ConfigureHeader(
        bool allowMove = true,
        bool allowClose = true,
        bool allowMinimize = false,
        bool allowMaximize = true,
        bool allowHelp = false,
        bool allowThemeToggle = true)
    {
        _header.AllowMove = allowMove;
        _header.AllowClose = allowClose;
        _header.AllowMinimize = allowMinimize;
        _header.AllowMaximize = allowMaximize;
        _header.AllowHelp = allowHelp;
        _header.AllowThemeToggle = allowThemeToggle;
    }

    protected void SetHeaderText(string title, string subtitle = "")
    {
        _header.Title = title;
        _header.Subtitle = subtitle;
        Text = title;
    }

    protected void SetBodyPadding(Padding padding)
    {
        _bodyPadding = padding;
        _bodyPanel.Padding = padding;
    }

    protected virtual void OnThemeChanged(HiveThemeDefinition theme)
    {
    }

    public void SetThemeManager(IHiveThemeManager themeManager)
    {
        ArgumentNullException.ThrowIfNull(themeManager);

        if (ReferenceEquals(_themeManager, themeManager))
            return;

        _themeManager.ThemeChanged -= ThemeManagerOnThemeChanged;
        _themeManager = themeManager;
        _themeManager.ThemeChanged += ThemeManagerOnThemeChanged;
        ApplyTheme();
        OnThemeChanged(_themeManager.Theme);
    }

    protected virtual void OnHeaderHelp()
    {
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _themeManager.ThemeChanged -= ThemeManagerOnThemeChanged;
        base.OnFormClosed(e);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateWindowRegion();
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmNcHitTest &&
            WindowState == FormWindowState.Normal &&
            FormBorderStyle == FormBorderStyle.None)
        {
            base.WndProc(ref message);

            if ((long)message.Result == HtClient)
            {
                var point = PointToClient(GetScreenPoint(message.LParam));
                var hitTest = GetResizeHitTest(point);

                if (hitTest != HtClient)
                {
                    message.Result = (IntPtr)hitTest;
                    return;
                }
            }

            return;
        }

        base.WndProc(ref message);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _themeManager.ThemeChanged -= ThemeManagerOnThemeChanged;

            Region = null;
            _windowRegion?.Dispose();
            _windowRegion = null;
        }

        base.Dispose(disposing);

        if (disposing)
            _formFont.Dispose();
    }

    private void ApplyTheme()
    {
        var theme = _themeManager.Theme;

        BackColor = theme.Palette.Border;
        ForeColor = theme.Palette.Text;
        if (!string.Equals(
                _formFont.FontFamily.Name,
                theme.Typography.FontFamily,
                StringComparison.Ordinal) ||
            Math.Abs(_formFont.Size - theme.Typography.BodySize) > 0.01f)
        {
            _formFont.Dispose();
            _formFont = new Font(
                theme.Typography.FontFamily,
                theme.Typography.BodySize);
            Font = _formFont;
        }

        if (_bodyPanel.BackColor != theme.Palette.Surface)
            _bodyPanel.BackColor = theme.Palette.Surface;

        // Geometry is established during construction/configuration.
        // Theme changes must not rewrite padding or dimensions.
        _header.ApplyTheme(theme);

        _themeManager.Apply(_bodyPanel);
    }

    private void ThemeManagerOnThemeChanged(object? sender, EventArgs e)
    {
        ApplyTheme();
        OnThemeChanged(_themeManager.Theme);
    }

    private void HeaderOnHelpClicked(object? sender, EventArgs e) =>
        OnHeaderHelp();

    private static Point GetScreenPoint(IntPtr lParam)
    {
        var value = unchecked((long)lParam);

        return new Point(
            unchecked((short)(value & 0xFFFF)),
            unchecked((short)((value >> 16) & 0xFFFF)));
    }

    private int GetResizeHitTest(Point point)
    {
        var left = point.X >= 0 && point.X < ResizeGripSize;
        var right = point.X >= Math.Max(0, ClientSize.Width - ResizeGripSize);
        var top = point.Y >= 0 && point.Y < ResizeGripSize;
        var bottom = point.Y >= Math.Max(0, ClientSize.Height - ResizeGripSize);

        if (left && top)
            return HtTopLeft;

        if (right && top)
            return HtTopRight;

        if (left && bottom)
            return HtBottomLeft;

        if (right && bottom)
            return HtBottomRight;

        if (left)
            return HtLeft;

        if (right)
            return HtRight;

        if (top)
            return HtTop;

        if (bottom)
            return HtBottom;

        return HtClient;
    }

    private void UpdateWindowRegion()
    {
        if (!IsHandleCreated || Width <= 0 || Height <= 0)
            return;

        // Maximized WinForms windows should use the full client rectangle.
        // Keeping the rounded Region while maximized clips the screen corners
        // and can interfere with native resize/snap behavior.
        if (WindowState == FormWindowState.Maximized)
        {
            Region = null;
            _windowRegion?.Dispose();
            _windowRegion = null;
            return;
        }

        var radius = Math.Min(
            Math.Max(1, CornerRadius),
            Math.Min(Width, Height) / 2);

        using var path = CreateRoundedPath(
            new RectangleF(
                0.5f,
                0.5f,
                Width - 1f,
                Height - 1f),
            radius);

        var nextRegion = new Region(path);
        var previousRegion = _windowRegion;
        _windowRegion = nextRegion;
        Region = nextRegion;
        previousRegion?.Dispose();
    }

    private static GraphicsPath CreateRoundedPath(
        RectangleF bounds,
        float radius)
    {
        var diameter = Math.Min(
            radius * 2f,
            Math.Min(bounds.Width, bounds.Height));

        var path = new GraphicsPath();

        path.AddArc(
            new RectangleF(bounds.X, bounds.Y, diameter, diameter),
            180f,
            90f);
        path.AddArc(
            new RectangleF(bounds.Right - diameter, bounds.Y, diameter, diameter),
            270f,
            90f);
        path.AddArc(
            new RectangleF(
                bounds.Right - diameter,
                bounds.Bottom - diameter,
                diameter,
                diameter),
            0f,
            90f);
        path.AddArc(
            new RectangleF(bounds.X, bounds.Bottom - diameter, diameter, diameter),
            90f,
            90f);
        path.CloseFigure();

        return path;
    }
}
