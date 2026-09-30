using System.Drawing;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms;

internal sealed class HiveAdvancedOverviewPage : UserControl, IHiveAdvancedConfigurationPage
{
    private readonly IHiveThemeManager _themeManager;

    public HiveAdvancedOverviewPage(IHiveThemeManager themeManager)
    {
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = new Padding(20);

        var fallbackFont = SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;

        var title = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 42,
            Text = "Advanced Provider Configuration",
            Font = new Font(fallbackFont, FontStyle.Bold),
            AccessibleName = "Advanced Provider Configuration overview"
        };

        var description = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 70,
            Text =
                "Advanced Provider Configuration is the administrative view of the Provider → ProviderAccount → ExecutionTarget graph. " +
                "Normal Providers onboarding remains the simplified entry point.",
            Padding = new Padding(0, 4, 0, 12)
        };

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            AutoScroll = true,
            Padding = new Padding(0, 4, 0, 0)
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        body.Controls.Add(
            CreateSection(
                "Resource relationship",
                "Provider defines identity and transport. ProviderAccount represents an account/credential boundary. " +
                "ExecutionTarget defines the concrete endpoint plus model/deployment and the durable capability configuration boundary."),
            0,
            0);
        body.Controls.Add(
            CreateSection(
                "Automatic versus Manual targets",
                "Automatic ExecutionTargets are maintained from successful provider discovery. Manual targets remain administrator-owned and are not overwritten by discovery."),
            0,
            1);
        body.Controls.Add(
            CreateSection(
                "Discovery versus configuration",
                "Provider model discovery is observational evidence. Model Information shows what the provider reported. " +
                "Configured ExecutionTarget capability entries remain authoritative when explicitly configured."),
            0,
            2);
        body.Controls.Add(
            CreateSection(
                "Normal Providers page",
                "Use the normal Providers page for built-in onboarding and explicit provider Refresh. It collects only the provider identity and credential required by the built-in catalog."),
            0,
            3);
        body.Controls.Add(
            CreateSection(
                "Advanced administration",
                "Use Providers, Accounts / Credentials, and Execution Targets for multiple accounts, custom endpoints, local/self-hosted services, manual targets, and administrative lifecycle control. " +
                "Use Model Information to inspect the latest successful provider model observation."),
            0,
            4);

        Controls.Add(body);
        Controls.Add(description);
        Controls.Add(title);

        _themeManager.ThemeChanged += ThemeManagerOnChanged;
        ApplyTheme();
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private Control CreateSection(string heading, string text)
    {
        var fallbackFont = SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;

        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 112,
            Padding = new Padding(0, 6, 0, 10),
            Margin = new Padding(0, 0, 0, 6)
        };

        var headingLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = heading,
            Font = new Font(fallbackFont, FontStyle.Bold)
        };

        var bodyLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Text = text,
            Padding = new Padding(0, 2, 0, 0)
        };

        panel.Controls.Add(bodyLabel);
        panel.Controls.Add(headingLabel);
        return panel;
    }

    private void ThemeManagerOnChanged(object? sender, EventArgs e) => ApplyTheme();

    private void ApplyTheme()
    {
        if (IsDisposed || Disposing)
            return;

        _themeManager.Apply(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _themeManager.ThemeChanged -= ThemeManagerOnChanged;

        base.Dispose(disposing);
    }
}