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
        Overview,
        Providers,
        Accounts,
        ExecutionTargets,
        ModelInformation
    }

    private sealed record NavigationEntry(
        string Title,
        AdvancedPage Page);

    private readonly IHiveManagementFacade _management;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveThemeManager _themeManager;
    private readonly IHiveExampleOutput? _output;
    private readonly HiveNavigationTree _navigation;
    private readonly SplitContainer _navigationSplit;
    private readonly Panel _contentHost;
    private readonly Dictionary<AdvancedPage, TreeNode> _nodes = new();
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
            "Administrative Provider, Account / Credential, Execution Target, and Model Information management.",
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

        _navigation = new HiveNavigationTree
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            HideSelection = false,
            FullRowSelect = true,
            ShowLines = false,
            ShowPlusMinus = false,
            ShowRootLines = false,
            AccessibleName = "Advanced Provider Configuration navigation"
        };

        AddNavigation(new NavigationEntry("Overview", AdvancedPage.Overview));
        AddNavigation(new NavigationEntry("Providers", AdvancedPage.Providers));
        AddNavigation(new NavigationEntry("Accounts / Credentials", AdvancedPage.Accounts));
        AddNavigation(new NavigationEntry("Execution Targets", AdvancedPage.ExecutionTargets));
        AddNavigation(new NavigationEntry("Model Information", AdvancedPage.ModelInformation));

        _contentHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 0, 0, 0)
        };

        _navigationSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.Panel1,
            IsSplitterFixed = true,
            SplitterWidth = 1,
            Panel1MinSize = 320,
            Panel2MinSize = 300,
            SplitterDistance = 320
        };
        _navigationSplit.Panel1.Padding = new Padding(4);
        _navigationSplit.Panel2.Padding = new Padding(4);
        _navigationSplit.Panel1.Controls.Add(_navigation);
        _navigationSplit.Panel2.Controls.Add(_contentHost);

        BodyPanel.Controls.Add(_navigationSplit);
        BodyPanel.PerformLayout();
        _navigationSplit.PerformLayout();
        _navigationSplit.SplitterDistance = 320;
        ThemeManager.Apply(BodyPanel);

        _navigation.AfterSelect += NavigationAfterSelect;
        _themeManager.ThemeChanged += ThemeManagerOnChanged;

        if (_nodes.TryGetValue(AdvancedPage.Overview, out var overviewNode))
            _navigation.SelectedNode = overviewNode;

        Load += async (_, _) => await SelectCurrentPageAsync().ConfigureAwait(true);
    }

    internal TreeView NavigationTree => _navigation;

    internal int NavigationSplitterDistance => _navigationSplit.SplitterDistance;

    internal Panel ContentHost => _contentHost;

    private void AddNavigation(NavigationEntry entry)
    {
        var node = new TreeNode(entry.Title)
        {
            Tag = entry.Page,
            Name = entry.Page.ToString(),
            ToolTipText = entry.Page == AdvancedPage.ModelInformation
                ? "Read-only provider model discovery information."
                : entry.Title
        };

        _nodes.Add(entry.Page, node);
        _navigation.Nodes.Add(node);
    }

    private async void NavigationAfterSelect(object? sender, TreeViewEventArgs e)
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

        if (_navigation.SelectedNode?.Tag is not AdvancedPage page)
            return;

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
            _contentHost.Controls.Add(view);
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
            AdvancedPage.Overview => new HiveAdvancedOverviewPage(_themeManager),
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
            AdvancedPage.ModelInformation => new HiveModelInformationSettingsView(
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

        if (_contentHost.Controls.Contains(page))
            _contentHost.Controls.Remove(page);

        page.Dispose();
    }

    private void ThemeManagerOnChanged(object? sender, EventArgs e)
    {
        if (IsDisposed || Disposing)
            return;

        _themeManager.Apply(_navigation);
        _themeManager.Apply(_contentHost);

        if (_currentPage is not null)
            _themeManager.Apply(_currentPage);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _navigation.AfterSelect -= NavigationAfterSelect;
            _themeManager.ThemeChanged -= ThemeManagerOnChanged;

            var cts = Interlocked.Exchange(ref _pageCts, null);
            cts?.Cancel();
            cts?.Dispose();

            DisposeCurrentPage();
        }

        base.Dispose(disposing);
    }
}
