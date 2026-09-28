using System.Drawing;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
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
    private readonly HashSet<int> _initializedTabs = new();

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

        _tabs.SelectedIndexChanged += TabsOnSelectedIndexChanged;

        BodyPanel.Controls.Add(_tabs);
        ThemeManager.Apply(BodyPanel);

        Load += async (_, _) => await InitializeAsync();
    }

    private async void TabsOnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (IsDisposed || Disposing)
            return;

        try
        {
            await InitializeSelectedTabAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
            when (_lifetimeCts?.IsCancellationRequested == true || IsDisposed || Disposing)
        {
        }
        catch (Exception exception)
        {
            if (!IsDisposed && !Disposing)
            {
                HiveUiErrorReporter.Report(
                    this,
                    exception,
                    "Advanced Provider Configuration",
                    "The selected advanced configuration page could not be initialized.",
                    _output,
                    _themeManager);
            }
        }
    }

    private async Task InitializeSelectedTabAsync()
    {
        var index = _tabs.SelectedIndex;
        if (index < 0 || _initializedTabs.Contains(index))
            return;

        var token = (_lifetimeCts ??= new CancellationTokenSource()).Token;

        switch (index)
        {
            case 0:
                await _providers.InitializeAsync(token).ConfigureAwait(true);
                break;

            case 1:
                await _accounts.InitializeAsync(token).ConfigureAwait(true);
                break;

            case 2:
                await _targets.InitializeAsync(token).ConfigureAwait(true);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown advanced provider configuration tab '{index}'.");
        }

        _initializedTabs.Add(index);
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
            await InitializeSelectedTabAsync().ConfigureAwait(true);
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

            _tabs.SelectedIndexChanged -= TabsOnSelectedIndexChanged;
        }

        base.Dispose(disposing);
    }
}
