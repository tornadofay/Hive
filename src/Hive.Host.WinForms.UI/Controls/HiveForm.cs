using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

public abstract class HiveForm : Form
{
    private const int HeaderHeight = 54;
    private const int CornerRadius = 14;

    private readonly HiveWindowHeader _header;
    private readonly Panel _bodyPanel;
    private IHiveThemeManager _themeManager;
    private Font _formFont;
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
        Padding = new Padding(1);
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

        _bodyPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(_themeManager.Theme.Spacing.Xl),
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
        bool allowHelp = false)
    {
        _header.AllowMove = allowMove;
        _header.AllowClose = allowClose;
        _header.AllowMinimize = allowMinimize;
        _header.AllowHelp = allowHelp;
    }

    protected void SetHeaderText(string title, string subtitle = "")
    {
        _header.Title = title;
        _header.Subtitle = subtitle;
        Text = title;
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _themeManager.ThemeChanged -= ThemeManagerOnThemeChanged;

            Region = null;
            _windowRegion?.Dispose();
            _windowRegion = null;
            _formFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private void ApplyTheme()
    {
        var theme = _themeManager.Theme;

        BackColor = theme.Palette.WindowBackground;
        ForeColor = theme.Palette.Text;
        var nextFont = new Font(
            theme.Typography.FontFamily,
            theme.Typography.BodySize);
        var previousFont = _formFont;
        _formFont = nextFont;
        Font = nextFont;
        previousFont.Dispose();

        _bodyPanel.BackColor = theme.Palette.Surface;
        _bodyPanel.Padding = new Padding(theme.Spacing.Xl);

        _header.Height = HeaderHeight;
        _header.ApplyTheme(theme);

        _themeManager.Apply(_bodyPanel);
    }

    private void ThemeManagerOnThemeChanged(object? sender, EventArgs e) =>
        ApplyTheme();

    private void HeaderOnHelpClicked(object? sender, EventArgs e) =>
        OnHeaderHelp();

    private void UpdateWindowRegion()
    {
        if (!IsHandleCreated || Width <= 0 || Height <= 0)
            return;

        var radius = Math.Min(
            Math.Max(1, CornerRadius),
            Math.Min(Width, Height) / 2);

        using var path = CreateRoundedPath(
            new Rectangle(0, 0, Width, Height),
            radius);

        var nextRegion = new Region(path);
        var previousRegion = _windowRegion;
        _windowRegion = nextRegion;
        Region = nextRegion;
        previousRegion?.Dispose();
    }

    private static GraphicsPath CreateRoundedPath(
        Rectangle bounds,
        int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();

        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180f, 90f);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270f, 90f);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0f, 90f);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90f, 90f);
        path.CloseFigure();

        return path;
    }
}
