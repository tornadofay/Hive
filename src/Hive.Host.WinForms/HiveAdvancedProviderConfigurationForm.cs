using System.Drawing;
using Hive.Core;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Host.WinForms;

internal sealed class HiveAdvancedProviderConfigurationForm : HiveForm
{
    private readonly IHiveManagementFacade _management;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveThemeManager _themeManager;
    private readonly IHiveExampleOutput? _output;
    private readonly TabControl _tabs;
    private readonly HiveProviderConfigurationView _providers;
    private readonly HiveProviderAccountsSettingsView _accounts;
    private readonly HiveExecutionTargetsSettingsView _targets;
    private CancellationTokenSource? _lifetimeCts;

    public HiveAdvancedProviderConfigurationForm(
        IHiveManagementFacade management,
        ResourceAccessContext accessContext,
        IHiveThemeManager themeManager,
        IHiveExampleOutput? output = null)
        : base(
            "Advanced Provider Configuration",
            "Advanced administration of Providers, Accounts / Credentials, and Execution Targets.",
            new Size(1060, 720),
            new Size(820, 560),
            themeManager)
    {
        _management = management ?? throw new ArgumentNullException(nameof(management));
        _accessContext = accessContext ?? throw new ArgumentNullException(nameof(accessContext));
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _output = output;

        ConfigureHeader(
            allowMove: true,
            allowClose: true,
            allowMinimize: false,
            allowMaximize: true,
            allowHelp: false,
            allowThemeToggle: true);

        SetBodyPadding(new Padding(12));

        _providers = new HiveProviderConfigurationView(
            _management,
            _accessContext,
            _themeManager,
            _output);
        _accounts = new HiveProviderAccountsSettingsView(
            _management,
            _accessContext,
            _themeManager,
            _output);
        _targets = new HiveExecutionTargetsSettingsView(
            _management,
            _accessContext,
            _themeManager,
            _output);

        _tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(12, 6)
        };

        AddTab("Providers", _providers);
        AddTab("Accounts / Credentials", _accounts);
        AddTab("Execution Targets", _targets);

        BodyPanel.Controls.Add(_tabs);
        ThemeManager.Apply(BodyPanel);

        Load += async (_, _) => await InitializeAsync();
    }

    private void AddTab(string title, Control page)
    {
        var tab = new TabPage(title)
        {
            Padding = new Padding(8),
            UseVisualStyleBackColor = false
        };
        tab.Controls.Add(page);
        page.Dock = DockStyle.Fill;
        _tabs.TabPages.Add(tab);
    }

    private async Task InitializeAsync()
    {
        if (IsDisposed || Disposing)
            return;

        _lifetimeCts ??= new CancellationTokenSource();

        try
        {
            await _providers.InitializeAsync(_lifetimeCts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
            when (_lifetimeCts.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            HiveUiErrorReporter.Report(
                this,
                exception,
                "Advanced Provider Configuration",
                "The advanced provider configuration could not be initialized.",
                _output,
                _themeManager);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            var cts = _lifetimeCts;
            _lifetimeCts = null;
            cts?.Cancel();
            cts?.Dispose();

            _providers.Dispose();
            _accounts.Dispose();
            _targets.Dispose();
        }

        base.Dispose(disposing);
    }
}
