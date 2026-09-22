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
    private CancellationTokenSource? _initializationCts;

    public HiveSettingsView(
        IHiveManagementFacade management,
        ResourceAccessContext accessContext,
        IHiveThemeManager themeManager,
        string? applicationName = null)
    {
        _management = management ?? throw new ArgumentNullException(nameof(management));
        _accessContext = accessContext ?? throw new ArgumentNullException(nameof(accessContext));
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _applicationName = string.IsNullOrWhiteSpace(applicationName)
            ? HivePersistenceConfiguration.DefaultApplicationName
            : applicationName.Trim();

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = new Padding(16);

        _titleFont = new Font(
            themeManager.Theme.Typography.FontFamily,
            15f,
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
            Height = 42,
            Text =
                "Global Hive package configuration. Each leaf owns one configuration domain; resource domains use CRUD and global settings use dedicated editors.",
            Margin = new Padding(0, 4, 0, 12),
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
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 212));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        _navigation = new HiveNavigationTree
        {
            Dock = DockStyle.Fill,
            AccessibleName = "Hive Settings navigation",
            AccessibleDescription =
                "Navigate Hive package configuration by Providers, Agents, and Persistence."
        };
        _navigation.AfterSelect += NavigationAfterSelect;

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
        navigationRoot.Nodes.Add(
            CreatePageNode(
                "Persistence",
                "One global SQL Server / LocalDB configuration editor and connection test.",
                SettingsPageKey.Persistence));

        _navigation.Nodes.Add(navigationRoot);
        navigationRoot.Expand();
        providersNode.Expand();

        _content = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 0, 0, 0),
            AccessibleName = "Settings content"
        };

        body.Controls.Add(_navigation, 0, 0);
        body.Controls.Add(_content, 1, 0);

        Controls.Add(body);
        Controls.Add(_description);
        Controls.Add(_title);

        _themeManager.Apply(this);

        _navigation.SelectedNode = providersNode.Nodes[0];
        Load += async (_, _) => await InitializeAsync();
    }

    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        _initializationCts?.Cancel();
        _initializationCts?.Dispose();
        _initializationCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);

        try
        {
            _providerConfigurationView ??= new HiveProviderConfigurationView(
                _management,
                _accessContext,
                _themeManager);

            _providerAccountsView ??= new HiveProviderAccountsSettingsView(
                _management,
                _accessContext,
                _themeManager);

            _executionTargetsView ??= new HiveExecutionTargetsSettingsView(
                _management,
                _accessContext,
                _themeManager);

            _agentView ??= new HiveAgentSettingsView(
                _management,
                _accessContext,
                _themeManager);

            _persistenceView ??= new HivePersistenceSettingsView(
                _management,
                _accessContext,
                _themeManager,
                _applicationName);

            await _persistenceView.InitializeAsync(
                _initializationCts.Token).ConfigureAwait(true);
            await _providerConfigurationView.InitializeAsync(
                _initializationCts.Token).ConfigureAwait(true);
            await _providerAccountsView.InitializeAsync(
                _initializationCts.Token).ConfigureAwait(true);
            await _executionTargetsView.InitializeAsync(
                _initializationCts.Token).ConfigureAwait(true);
            await _agentView.InitializeAsync(
                _initializationCts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
            when (_initializationCts.IsCancellationRequested)
        {
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _navigation.AfterSelect -= NavigationAfterSelect;
            _initializationCts?.Cancel();
            _initializationCts?.Dispose();
            _providerConfigurationView?.Dispose();
            _providerAccountsView?.Dispose();
            _executionTargetsView?.Dispose();
            _agentView?.Dispose();
            _persistenceView?.Dispose();
            _titleFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private void NavigationAfterSelect(
        object? sender,
        TreeViewEventArgs e)
    {
        if (e.Node?.Tag is not SettingsPage page)
            return;

        ShowSelectedPage(page);
    }

    private void ShowSelectedPage(SettingsPage page)
    {
        Control control = page.Key switch
        {
            SettingsPageKey.ProviderConfiguration => _providerConfigurationView ??=
                new HiveProviderConfigurationView(
                    _management,
                    _accessContext,
                    _themeManager),

            SettingsPageKey.ProviderAccounts => _providerAccountsView ??=
                new HiveProviderAccountsSettingsView(
                    _management,
                    _accessContext,
                    _themeManager),

            SettingsPageKey.ExecutionTargets => _executionTargetsView ??=
                new HiveExecutionTargetsSettingsView(
                    _management,
                    _accessContext,
                    _themeManager),

            SettingsPageKey.Agents => _agentView ??=
                new HiveAgentSettingsView(
                    _management,
                    _accessContext,
                    _themeManager),

            SettingsPageKey.Persistence => _persistenceView ??=
                new HivePersistenceSettingsView(
                    _management,
                    _accessContext,
                    _themeManager,
                    _applicationName),

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
