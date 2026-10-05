using System.Drawing;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Host.WinForms;

public sealed class HiveAdvancedProviderConfigurationForm : HiveForm
{
    private enum AdvancedPage
    {
        Providers,
        Accounts,
        ExecutionTargets
    }

    private sealed record NavigationEntry(
        string Title,
        AdvancedPage Page);

    private readonly IHiveManagementFacade _management;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveThemeManager _themeManager;
    private readonly IHiveExampleOutput? _output;
    private readonly HiveTabControl _navigationTabs;
    private readonly Dictionary<AdvancedPage, TabPage> _tabs = new();
    private CancellationTokenSource? _pageCts;
    private Control? _currentPage;
    private int _pageRequestVersion;

    public HiveAdvancedProviderConfigurationForm(
        IHiveManagementFacade management,
        ResourceAccessContext accessContext,
        IHiveThemeManager themeManager,
        IHiveExampleOutput? output = null)
        : base(
            "Advanced Provider Configuration",
            "Administrative Provider, Account / Credential, and Execution Target management.",
            new Size(1160, 760),
            new Size(900, 620),
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

        _navigationTabs = new HiveTabControl
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            AccessibleName = "Advanced Provider Configuration tabs",
            AccessibleRole = AccessibleRole.PageTabList
        };

        AddTab(new NavigationEntry("Providers", AdvancedPage.Providers));
        AddTab(new NavigationEntry("Accounts / Credentials", AdvancedPage.Accounts));
        AddTab(new NavigationEntry("Execution Targets", AdvancedPage.ExecutionTargets));

        BodyPanel.Controls.Add(_navigationTabs);
        BodyPanel.PerformLayout();

        ThemeManager.Apply(BodyPanel);

        _navigationTabs.SelectedIndexChanged += NavigationTabChanged;
        _themeManager.ThemeChanged += ThemeManagerOnChanged;

        Load += async (_, _) => await SelectCurrentPageAsync().ConfigureAwait(true);
    }

    internal HiveTabControl NavigationTabs => _navigationTabs;

    private void AddTab(NavigationEntry entry)
    {
        var tab = _navigationTabs.TabPages.Add(entry.Title);
        tab.Name = entry.Page.ToString();
        tab.Tag = entry.Page;
        tab.Padding = new Padding(4, 12, 4, 4);

        _tabs.Add(entry.Page, tab);
    }

    private async void NavigationTabChanged(object? sender, EventArgs e)
    {
        if (IsDisposed ||
            Disposing)
        {
            return;
        }

        try
        {
            await SelectCurrentPageAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
            when (_pageCts?.IsCancellationRequested == true || IsDisposed || Disposing)
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
                    "The selected advanced configuration page could not be displayed.",
                    _output,
                    _themeManager);
            }
        }
    }

    private async Task SelectCurrentPageAsync()
    {
        if (IsDisposed || Disposing)
            return;

        if (_navigationTabs.SelectedTab?.Tag is not AdvancedPage page ||
            !_tabs.TryGetValue(page, out var tab))
        {
            return;
        }

        var version = Interlocked.Increment(ref _pageRequestVersion);
        var pageCts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _pageCts, pageCts);
        previous?.Cancel();

        DisposeCurrentPage();

        try
        {
            var view = CreatePage(page);
            if (view is null)
                throw new InvalidOperationException($"No Advanced Provider Configuration page is registered for '{page}'.");

            view.Dock = DockStyle.Fill;
            tab.Controls.Add(view);
            _currentPage = view;
            _themeManager.Apply(view);

            if (view is IHiveAdvancedConfigurationPage initializable)
            {
                await initializable
                    .InitializeAsync(pageCts.Token)
                    .ConfigureAwait(true);
            }

            if (version != Volatile.Read(ref _pageRequestVersion) ||
                pageCts.IsCancellationRequested ||
                IsDisposed ||
                Disposing)
            {
                return;
            }
        }
        catch
        {
            DisposeCurrentPage();
            throw;
        }
        finally
        {
            if (ReferenceEquals(_pageCts, pageCts))
                Interlocked.CompareExchange(ref _pageCts, null, pageCts);

            pageCts.Dispose();
        }
    }

    private Control CreatePage(AdvancedPage page) =>
        page switch
        {
            AdvancedPage.Providers => new HiveProviderConfigurationView(
                _management,
                _accessContext,
                _themeManager,
                _output),
            AdvancedPage.Accounts => new HiveProviderAccountsSettingsView(
                _management,
                _accessContext,
                _themeManager,
                _output),
            AdvancedPage.ExecutionTargets => new HiveExecutionTargetsSettingsView(
                _management,
                _accessContext,
                _themeManager,
                _output),
            _ => throw new ArgumentOutOfRangeException(nameof(page), page, null)
        };

    private void DisposeCurrentPage()
    {
        var page = Interlocked.Exchange(ref _currentPage, null);
        if (page is null)
            return;

        if (page.Parent is not null)
            page.Parent.Controls.Remove(page);

        page.Dispose();
    }

    private void ThemeManagerOnChanged(object? sender, EventArgs e)
    {
        if (IsDisposed || Disposing)
            return;

        _themeManager.Apply(_navigationTabs);

        if (_currentPage is not null)
            _themeManager.Apply(_currentPage);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _navigationTabs.SelectedIndexChanged -= NavigationTabChanged;
            _themeManager.ThemeChanged -= ThemeManagerOnChanged;

            var cts = Interlocked.Exchange(ref _pageCts, null);
            cts?.Cancel();
            cts?.Dispose();

            DisposeCurrentPage();
        }

        base.Dispose(disposing);
    }
}
