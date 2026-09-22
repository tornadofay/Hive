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
    private readonly HiveNavigationTree _navigation;
    private readonly Panel _content;
    private readonly Label _title;
    private readonly Label _description;
    private readonly Font _titleFont;

    private HiveProviderSettingsView? _providerView;
    private HiveAgentSettingsView? _agentView;
    private HivePersistenceSettingsView? _persistenceView;
    private CancellationTokenSource? _initializationCts;

    public HiveSettingsView(
        IHiveManagementFacade management,
        ResourceAccessContext accessContext,
        IHiveThemeManager themeManager)
    {
        _management = management ?? throw new ArgumentNullException(nameof(management));
        _accessContext = accessContext ?? throw new ArgumentNullException(nameof(accessContext));
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));

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
                "Global Hive package configuration. Settings pages use Hive.Management; database, secret, provider, and execution implementation details remain outside the UI.",
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
                "Providers",
                "Provider identity, transport, endpoint defaults, and connection testing.",
                SettingsPageKey.Providers));
        providersNode.Nodes.Add(
            CreatePageNode(
                "Accounts / Credentials",
                "Advanced provider credential/account management. No provider login is required.",
                SettingsPageKey.ProviderAccounts));
        providersNode.Nodes.Add(
            CreatePageNode(
                "Execution Targets",
                "Advanced model/deployment endpoints and capability-aware execution targets.",
                SettingsPageKey.ExecutionTargets));

        navigationRoot.Nodes.Add(providersNode);
        navigationRoot.Nodes.Add(
            CreatePageNode(
                "Agents",
                "AgentDefinitions and their configured execution target references.",
                SettingsPageKey.Agents));
        navigationRoot.Nodes.Add(
            CreatePageNode(
                "Persistence",
                "SQL Server / LocalDB configuration and database/schema state.",
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
            if (_providerView is null)
            {
                _providerView = new HiveProviderSettingsView(
                    _management,
                    _accessContext,
                    _themeManager);
                _themeManager.Apply(_providerView);
            }

            if (_agentView is null)
            {
                _agentView = new HiveAgentSettingsView(
                    _management,
                    _accessContext,
                    _themeManager);
                _themeManager.Apply(_agentView);
            }

            if (_persistenceView is null)
            {
                _persistenceView = new HivePersistenceSettingsView(
                    _management,
                    _accessContext,
                    _themeManager);
                _themeManager.Apply(_persistenceView);
            }

            await _persistenceView.InitializeAsync(
                _initializationCts.Token).ConfigureAwait(true);

            await _providerView.InitializeAsync(
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
            _providerView?.Dispose();
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
            SettingsPageKey.Providers or
            SettingsPageKey.ProviderAccounts or
            SettingsPageKey.ExecutionTargets => _providerView ??=
                new HiveProviderSettingsView(
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
                    _themeManager),

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

        if (control is HiveProviderSettingsView providerView)
        {
            providerView.FocusSection(
                page.Key switch
                {
                    SettingsPageKey.ProviderAccounts => HiveProviderSettingsSection.AccountsAndCredentials,
                    SettingsPageKey.ExecutionTargets => HiveProviderSettingsSection.ExecutionTargets,
                    _ => HiveProviderSettingsSection.Providers
                });
        }
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
        Providers,
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
