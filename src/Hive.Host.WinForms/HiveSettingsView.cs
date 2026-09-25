using System.Drawing;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Host.WinForms;

public sealed class HiveSettingsView : UserControl
{
    private readonly IHiveManagementFacade _management;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveThemeManager _themeManager;
    private readonly IHiveExampleOutput? _output;
    private readonly string _applicationName;
    private readonly HiveNavigationTree _navigation;
    private readonly Panel _content;
    private readonly Label _title;
    private readonly Label _description;
    private readonly Font _titleFont;

    private HiveProviderConfigurationView? _providerConfigurationView;
    private HiveProviderAccountsSettingsView? _providerAccountsView;
    private HiveExecutionTargetsSettingsView? _executionTargetsView;
    private HiveAgentSettingsView? _agentView;
    private HivePersistenceSettingsView? _persistenceView;
    private CancellationTokenSource? _lifetimeCts;
    private CancellationTokenSource? _navigationCts;
    private readonly HashSet<SettingsPageKey> _initializedPages = new();
    private readonly HashSet<SettingsPageKey> _initializingPages = new();

    public HiveSettingsView(
        IHiveManagementFacade management,
        ResourceAccessContext accessContext,
        IHiveThemeManager themeManager,
        string? applicationName = null,
        IHiveExampleOutput? output = null)
    {
        _management = management ?? throw new ArgumentNullException(nameof(management));
        _accessContext = accessContext ?? throw new ArgumentNullException(nameof(accessContext));
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _output = output;
        _applicationName = string.IsNullOrWhiteSpace(applicationName)
            ? HivePersistenceConfiguration.DefaultApplicationName
            : applicationName.Trim();

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = new Padding(16);

        _titleFont = new Font(
            themeManager.Theme.Typography.FontFamily,
            themeManager.Theme.Typography.TitleSize,
            FontStyle.Bold);

        _title = new Label
        {
            Text = "Hive Settings",
            Dock = DockStyle.Top,
            Height = 32,
            Font = _titleFont,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        _description = new Label
        {
            Dock = DockStyle.Top,
            Height = 34,
            Text =
                "Global Hive package configuration for providers, accounts, execution targets, agents, and persistence.",
            Margin = new Padding(0, 4, 0, 10),
            Padding = Padding.Empty,
            AutoEllipsis = true
        };

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 224));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        _navigation = new HiveNavigationTree
        {
            Dock = DockStyle.Fill,
            AccessibleName = "Hive Settings navigation",
            AccessibleDescription =
                "Navigate Hive package configuration by Providers, Agents, and Persistence."
        };

        var navigationRoot = new TreeNode("Hive Settings");

        var providersNode = new TreeNode("Providers");
        providersNode.Nodes.Add(
            CreatePageNode(
                "Provider Configuration",
                "CRUD for Provider resource identity and transport configuration.",
                SettingsPageKey.ProviderConfiguration));
        providersNode.Nodes.Add(
            CreatePageNode(
                "Accounts / Credentials",
                "CRUD for durable ProviderAccount resources and their Secret Store credential references.",
                SettingsPageKey.ProviderAccounts));
        providersNode.Nodes.Add(
            CreatePageNode(
                "Execution Targets",
                "CRUD for concrete endpoint/model/deployment targets and capability declarations.",
                SettingsPageKey.ExecutionTargets));

        navigationRoot.Nodes.Add(providersNode);
        navigationRoot.Nodes.Add(
            CreatePageNode(
                "Agents",
                "CRUD for AgentDefinitions and their configured ExecutionTarget references.",
                SettingsPageKey.Agents));
        var persistenceNode = CreatePageNode(
            "Persistence",
            "One global SQL Server / LocalDB configuration editor and connection test.",
            SettingsPageKey.Persistence);
        navigationRoot.Nodes.Add(persistenceNode);

        _navigation.Nodes.Add(navigationRoot);
        navigationRoot.Expand();
        providersNode.Expand();

        _content = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 0, 0, 0),
            AccessibleName = "Settings content"
        };

        body.Controls.Add(_navigation, 0, 0);
        body.Controls.Add(_content, 1, 0);

        Controls.Add(body);
        Controls.Add(_description);
        Controls.Add(_title);

        _themeManager.Apply(this);

        _navigation.AfterSelect += NavigationAfterSelect;
        _navigation.SelectedNode = persistenceNode;
        Load += async (_, _) => await InitializeAsync();
    }

    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        if (IsDisposed)
            return;

        _lifetimeCts ??= new CancellationTokenSource();

        _providerConfigurationView ??= new HiveProviderConfigurationView(
            _management,
            _accessContext,
            _themeManager,
            _output);

        _providerAccountsView ??= new HiveProviderAccountsSettingsView(
            _management,
            _accessContext,
            _themeManager,
            _output);

        _executionTargetsView ??= new HiveExecutionTargetsSettingsView(
            _management,
            _accessContext,
            _themeManager,
            _output);

        _agentView ??= new HiveAgentSettingsView(
            _management,
            _accessContext,
            _themeManager,
            _output);

        _persistenceView ??= new HivePersistenceSettingsView(
            _management,
            _accessContext,
            _themeManager,
            _applicationName,
            _output);

        // Persistence configuration is file/bootstrap-backed and must remain
        // usable even when the currently configured Hive SQL database is
        // unavailable. Database-backed resource pages initialize lazily when
        // the user navigates to them.
        await InitializePageAsync(
            SettingsPageKey.Persistence,
            cancellationToken).ConfigureAwait(true);
    }

    private async Task InitializePageAsync(
        SettingsPageKey key,
        CancellationToken cancellationToken)
    {
        if (_initializedPages.Contains(key) ||
            _initializingPages.Contains(key) ||
            IsDisposed)
        {
            return;
        }

        var lifetimeCts = _lifetimeCts ??= new CancellationTokenSource();

        _initializingPages.Add(key);

        using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(
            lifetimeCts.Token,
            cancellationToken);

        try
        {
            switch (key)
            {
                case SettingsPageKey.ProviderConfiguration:
                    await _providerConfigurationView!
                        .InitializeAsync(operationCts.Token)
                        .ConfigureAwait(true);
                    break;

                case SettingsPageKey.ProviderAccounts:
                    await _providerAccountsView!
                        .InitializeAsync(operationCts.Token)
                        .ConfigureAwait(true);
                    break;

                case SettingsPageKey.ExecutionTargets:
                    await _executionTargetsView!
                        .InitializeAsync(operationCts.Token)
                        .ConfigureAwait(true);
                    break;

                case SettingsPageKey.Agents:
                    await _agentView!
                        .InitializeAsync(operationCts.Token)
                        .ConfigureAwait(true);
                    break;

                case SettingsPageKey.Persistence:
                    await _persistenceView!
                        .InitializeAsync(operationCts.Token)
                        .ConfigureAwait(true);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unknown Settings page '{key}'.");
            }

            if (operationCts.IsCancellationRequested || IsDisposed || Disposing)
                return;

            _initializedPages.Add(key);
        }
        catch (OperationCanceledException)
            when (operationCts.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            if (!IsDisposed && !Disposing)
            {
                HiveUiErrorReporter.Report(
                    FindForm(),
                    exception,
                    "Hive Settings",
                    "The selected Settings page could not be initialized.",
                    _output,
                    _themeManager);
            }
        }
        finally
        {
            _initializingPages.Remove(key);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _navigation.AfterSelect -= NavigationAfterSelect;

            var navigationCts = Interlocked.Exchange(ref _navigationCts, null);
            navigationCts?.Cancel();

            var lifetimeCts = _lifetimeCts;
            _lifetimeCts = null;
            lifetimeCts?.Cancel();
            lifetimeCts?.Dispose();

            _providerConfigurationView?.Dispose();
            _providerAccountsView?.Dispose();
            _executionTargetsView?.Dispose();
            _agentView?.Dispose();
            _persistenceView?.Dispose();
            _titleFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private async void NavigationAfterSelect(
        object? sender,
        TreeViewEventArgs e)
    {
        if (e.Node?.Tag is not SettingsPage page ||
            IsDisposed ||
            Disposing)
        {
            return;
        }

        var lifetimeCts = _lifetimeCts;
        if (lifetimeCts is null || lifetimeCts.IsCancellationRequested)
            return;

        var navigationCts = CancellationTokenSource.CreateLinkedTokenSource(
            lifetimeCts.Token);

        var previous = Interlocked.Exchange(
            ref _navigationCts,
            navigationCts);
        previous?.Cancel();

        try
        {
            ShowSelectedPage(page);
            await RefreshPageOnNavigationAsync(
                page.Key,
                navigationCts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
            when (navigationCts.IsCancellationRequested ||
                  IsDisposed ||
                  Disposing)
        {
            return;
        }
        catch (Exception exception)
        {
            if (!IsDisposed && !Disposing)
            {
                HiveUiErrorReporter.Report(
                    FindForm(),
                    exception,
                    "Hive Settings",
                    "The selected Settings page could not be displayed.",
                    _output,
                    _themeManager);
            }
        }
        finally
        {
            if (ReferenceEquals(_navigationCts, navigationCts))
                Interlocked.CompareExchange(
                    ref _navigationCts,
                    null,
                    navigationCts);

            navigationCts.Dispose();
        }
    }

    private async Task RefreshPageOnNavigationAsync(
        SettingsPageKey key,
        CancellationToken cancellationToken)
    {
        if (IsDisposed)
            return;

        if (!_initializedPages.Contains(key))
        {
            await InitializePageAsync(
                key,
                cancellationToken).ConfigureAwait(true);
            return;
        }

        switch (key)
        {
            case SettingsPageKey.ProviderConfiguration:
                await _providerConfigurationView!
                    .InitializeAsync(cancellationToken)
                    .ConfigureAwait(true);
                break;

            case SettingsPageKey.ProviderAccounts:
                await _providerAccountsView!
                    .InitializeAsync(cancellationToken)
                    .ConfigureAwait(true);
                break;

            case SettingsPageKey.ExecutionTargets:
                await _executionTargetsView!
                    .InitializeAsync(cancellationToken)
                    .ConfigureAwait(true);
                break;

            case SettingsPageKey.Agents:
                await _agentView!
                    .InitializeAsync(cancellationToken)
                    .ConfigureAwait(true);
                break;

            case SettingsPageKey.Persistence:
                // Persistence is an editor and should not be reinitialized
                // on every navigation because that would overwrite unsaved
                // local edits.
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown Settings page '{key}'.");
        }
    }

    private void ShowSelectedPage(SettingsPage page)
    {
        Control control = page.Key switch
        {
            SettingsPageKey.ProviderConfiguration => _providerConfigurationView ??=
                new HiveProviderConfigurationView(
                    _management,
                    _accessContext,
                    _themeManager,
                    _output),

            SettingsPageKey.ProviderAccounts => _providerAccountsView ??=
                new HiveProviderAccountsSettingsView(
                    _management,
                    _accessContext,
                    _themeManager,
                    _output),

            SettingsPageKey.ExecutionTargets => _executionTargetsView ??=
                new HiveExecutionTargetsSettingsView(
                    _management,
                    _accessContext,
                    _themeManager,
                    _output),

            SettingsPageKey.Agents => _agentView ??=
                new HiveAgentSettingsView(
                    _management,
                    _accessContext,
                    _themeManager,
                    _output),

            SettingsPageKey.Persistence => _persistenceView ??=
                new HivePersistenceSettingsView(
                    _management,
                    _accessContext,
                    _themeManager,
                    _applicationName,
                    _output),

            _ => throw new InvalidOperationException(
                $"Unknown Settings page '{page.Key}'.")
        };

        _content.SuspendLayout();
        try
        {
            _content.Controls.Clear();
            control.Dock = DockStyle.Fill;
            _content.Controls.Add(control);
        }
        finally
        {
            _content.ResumeLayout(true);
        }

        _themeManager.Apply(control);
    }

    private static TreeNode CreatePageNode(
        string name,
        string description,
        SettingsPageKey key)
    {
        return new TreeNode(name)
        {
            Tag = new SettingsPage(key, name, description)
        };
    }

    private enum SettingsPageKey
    {
        ProviderConfiguration,
        ProviderAccounts,
        ExecutionTargets,
        Agents,
        Persistence
    }

    private sealed record SettingsPage(
        SettingsPageKey Key,
        string Name,
        string Description);
}
