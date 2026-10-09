using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Hive.Core;
using Hive.Example.WinForms;
using Hive.Host.WinForms;
using Hive.Management;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Xunit;

namespace Hive.Tests;

public sealed class HiveUiPolishTests
{
    [Fact]
    public void HiveExampleOutputView_NotifiesWhenOutputBecomesUnavailable()
    {
        using var output = new HiveExampleOutputView();
        var changeCount = 0;

        output.OutputAvailabilityChanged += (_, _) => changeCount++;

        output.OutputText = "output";
        output.OutputText = string.Empty;

        Assert.Equal(2, changeCount);
    }

    [Fact]
    public void HiveExampleOutputView_ClearNotifiesHostOfAvailabilityChange()
    {
        using var output = new HiveExampleOutputView();
        var changeCount = 0;

        output.OutputAvailabilityChanged += (_, _) => changeCount++;

        output.Write("TEST", "output");
        output.Clear();

        Assert.Equal(2, changeCount);
    }

    [Fact]
    public void HiveExampleTestSurface_DisablesCodeCopyWhenSnippetIsEmpty()
    {
        using var surface = new HiveExampleTestSurface();

        Assert.False(surface.CopyCodeButton.Enabled);

        surface.CodeSnippet = "// public API";

        Assert.True(surface.CopyCodeButton.Enabled);

        surface.CodeSnippet = string.Empty;

        Assert.False(surface.CopyCodeButton.Enabled);
    }

    [Fact]
    public void HiveExampleTestSurface_DisablesRunUntilConfiguredAndEnablesWhenConfigured()
    {
        using var surface = new HiveExampleTestSurface();

        Assert.False(surface.RunButton.Enabled);

        surface.ConfigureRun(_ => Task.CompletedTask);

        Assert.True(surface.RunButton.Enabled);
    }

    [Fact]
    public async Task HiveExampleTestSurface_ReportsSuccessfulRunState()
    {
        using var surface = new HiveExampleTestSurface();

        await surface.RunAsync(
            _ => Task.CompletedTask);

        Assert.Equal("Completed.", surface.StatusLabel.Text);
    }

    [Fact]
    public void HiveEditorLayout_KeepsSingleLineEditorsAtCompactHeight()
    {
        using var layout = new HiveEditorLayout();
        using var textBox = new TextBox();
        using var comboBox = new ComboBox();

        layout.Size = new Size(800, 400);
        layout.AddField("Name", "Name.", textBox);
        layout.AddField("Type", "Type.", comboBox);
        layout.CreateControl();
        layout.PerformLayout();
        layout.FieldsPanel.PerformLayout();

        var textHost = layout.FieldsPanel.GetControlFromPosition(1, 0);
        var comboHost = layout.FieldsPanel.GetControlFromPosition(1, 1);

        Assert.NotNull(textHost);
        Assert.NotNull(comboHost);
        Assert.IsType<TableLayoutPanel>(textHost);
        Assert.IsType<TableLayoutPanel>(comboHost);
        Assert.Equal(32, textBox.Height);
        Assert.True(comboBox.Height > 0);
        Assert.True(comboBox.Height <= 32);

        var textHostLayout = (TableLayoutPanel)textHost!;
        var comboHostLayout = (TableLayoutPanel)comboHost!;
        Assert.Equal(32, textHostLayout.GetRowHeights()[1]);
        Assert.Equal(32, comboHostLayout.GetRowHeights()[1]);
        Assert.Equal(DockStyle.Fill, textBox.Dock);
        Assert.Equal(DockStyle.None, comboBox.Dock);

        var expectedComboTop = (comboHost.Height - comboBox.Height) / 2;
        Assert.InRange(comboBox.Top, expectedComboTop - 1, expectedComboTop + 1);
    }

    [Fact]
    public void HiveEditorLayout_ClearFieldsDisposesOwnedEditors()
    {
        using var layout = new HiveEditorLayout();
        var editor = new TextBox();

        layout.AddField("Name", "Name.", editor);
        layout.ClearFields();

        Assert.True(editor.IsDisposed);
    }

    [Fact]
    public void HiveListPageLayout_SetContentDisposesPreviousContent()
    {
        using var layout = new HiveListPageLayout();
        var first = new Panel();
        var second = new Panel();

        layout.SetContent(first);
        layout.SetContent(second);

        Assert.True(first.IsDisposed);
        Assert.False(second.IsDisposed);
        Assert.Same(second, layout.ContentPanel.Controls[0]);
    }

    [Fact]
    public void HiveEditorLayout_PreservesFullHeightForMultilineEditors()
    {
        using var layout = new HiveEditorLayout();
        using var textBox = new TextBox
        {
            Multiline = true
        };

        layout.AddField("Description", "Description.", textBox, 150);

        var host = layout.FieldsPanel.GetControlFromPosition(1, 0);

        Assert.NotNull(host);
        Assert.IsType<Panel>(host);
        Assert.Equal(DockStyle.Fill, textBox.Dock);
        Assert.True(textBox.Height >= 32);
    }

    [Fact]
    public void HiveCrudPage_AppliesErrorStatusToneAndPreservesItAcrossThemeChanges()
    {
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);
        using var form = new TestHiveForm(themeManager);
        using var page = new HiveCrudPage<TestItem>();

        form.Body.Controls.Add(page);
        themeManager.Apply(form.Body);

        page.SetStatus("Operation failed.", HiveStatusTone.Error);

        Assert.Equal(
            themeManager.Theme.VisualStates.Error,
            page.StatusLabel.ForeColor);

        themeManager.SetMode(HiveThemeMode.Dark);

        Assert.Equal(
            themeManager.Theme.VisualStates.Error,
            page.StatusLabel.ForeColor);
    }

    [Fact]
    public void HiveCrudPage_AllowsProviderSpecificAddActionCaption()
    {
        using var page = new HiveCrudPage<TestItem>
        {
            EditItemAsync = (_, _) =>
                Task.FromResult<TestItem?>(new TestItem("created"))
        };

        page.AddButtonText = "Add Provider";

        var addButton = page.ActionBarPanel
            .Controls
            .OfType<TableLayoutPanel>()
            .SelectMany(static layout => layout.Controls.Cast<Control>())
            .OfType<FlowLayoutPanel>()
            .SelectMany(static flow => flow.Controls.Cast<Control>())
            .OfType<HiveButton>()
            .Single(button => string.Equals(button.Text, "Add Provider", StringComparison.Ordinal));

        Assert.Equal("Add Provider", addButton.Text);
        Assert.Equal("Add Provider", addButton.AccessibleName);
        Assert.Contains("new Provider", addButton.AccessibleDescription, StringComparison.Ordinal);
    }

    [Fact]
    public void HiveCrudPage_AlignsStatusFilterWhenToolbarBecomesCompact()
    {
        using var page = new HiveCrudPage<TestItem>();

        page.StatusSelector = item => item.Name;
        page.Size = new Size(420, 400);
        page.CreateControl();
        page.PerformLayout();

        var statusLabel = page.ActionBarPanel
            .Controls
            .OfType<TableLayoutPanel>()
            .SelectMany(static layout => layout.Controls.Cast<Control>())
            .OfType<FlowLayoutPanel>()
            .SelectMany(static flow => flow.Controls.Cast<Control>())
            .OfType<Label>()
            .FirstOrDefault(label =>
                string.Equals(label.Text, "Status", StringComparison.Ordinal));

        Assert.NotNull(statusLabel);
        Assert.Equal(0, statusLabel!.Margin.Left);

        page.Size = new Size(1200, 400);
        page.PerformLayout();

        Assert.Equal(16, statusLabel.Margin.Left);
    }

    [WinFormsFact]
    public async Task HivePersistenceSettingsView_UsesDatabaseSetupAndDataMigrationTabs()
    {
        var (management, managementProxy) =
            HiveWorkspaceLifecycleTests.ManagementFacadeProxy.Create();
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);

        using var host = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-2000, -2000),
            ClientSize = new Size(1180, 760),
            ShowInTaskbar = false
        };
        using var view = new HivePersistenceSettingsView(
            management,
            new ResourceAccessContext(
                DeploymentId.New(),
                TenantId.New(),
                PrincipalId.New()),
            themeManager,
            "Hive.TestHost")
        {
            Dock = DockStyle.Fill
        };
        host.Controls.Add(view);
        host.Show();
        Application.DoEvents();

        await view.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, view.NavigationTabs.TabPages.Count);
        Assert.Equal("Database Setup", view.NavigationTabs.TabPages[0].Text);
        Assert.Equal("Data Migration", view.NavigationTabs.TabPages[1].Text);
        Assert.Single(view.NavigationTabs.TabPages[1].Controls);

        view.NavigationTabs.SelectedIndex = 1;
        Application.DoEvents();

        var migrationView = Assert.IsType<HivePersistenceDataMigrationSettingsView>(
            view.NavigationTabs.TabPages[1].Controls[0]);
        await migrationView.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(managementProxy.PersistenceConnectionTestRequested.Task.IsCompleted);
        Assert.Contains(
            "select Refresh to test readiness",
            migrationView.StatusLabel.Text,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            managementProxy.PersistenceConfiguration.ServerName,
            migrationView.SourceSqlServerPicker.ServerName);
        Assert.Equal(
            managementProxy.PersistenceConfiguration.DatabaseName,
            migrationView.SourceDatabaseName);
        Assert.True(migrationView.SourceTrustServerCertificate);
        Assert.True(migrationView.SourceEncrypt);

        Assert.Equal(
            DockStyle.Fill,
            view.NavigationTabs.TabPages[1].Controls[0].Dock);
        Assert.Equal(2, view.BackendSelector.Items.Count);
        Assert.Equal("Embedded", view.BackendSelector.Items[0]?.ToString());
        Assert.Equal("SQL Server", view.BackendSelector.Items[1]?.ToString());

        Assert.InRange(view.BackendSelector.Width, 80, 120);
        Assert.InRange(view.DatabaseInput.Width, 240, 300);
        Assert.InRange(view.AuthenticationSelector.Width, 180, 240);
        Assert.Equal(1433, view.SqlPort);
        Assert.Equal(
            HivePersistenceConfiguration.BuildDatabaseName("Hive.TestHost"),
            view.DatabaseInput.Text);
        Assert.Equal(DockStyle.Fill, view.EmbeddedStorageInput.Dock);
        Assert.True(view.EmbeddedStorageInput.AutoSize);

        var embeddedPathRow =
            Assert.IsType<TableLayoutPanel>(view.EmbeddedStorageInput.Parent);
        Assert.True(embeddedPathRow.AutoSize);
        Assert.Equal(SizeType.AutoSize, embeddedPathRow.RowStyles[0].SizeType);

        view.NavigationTabs.SelectedIndex = 0;
        Application.DoEvents();

        view.SqlServerPicker.SetDiscoveredInstances(
            new[] { "localhost", @"localhost\HiveSql" },
            null);
        view.SqlServerPicker.ServerSelector.SelectedItem =
            view.SqlServerPicker.ServerSelector.Items[^1];

        view.PerformLayout();
        view.SqlServerPicker.PerformLayout();

        Assert.True(view.SqlServerPicker.IsCustomSelected);
        Assert.True(view.SqlServerPicker.CustomServerInput.Visible);
        Assert.True(view.SqlServerPicker.CustomServerInput.AutoSize);
        Assert.True(view.SqlServerPicker.CustomRow.AutoSize);
        Assert.Equal(
            SizeType.AutoSize,
            view.SqlServerPicker.CustomRow.RowStyles[0].SizeType);
        Assert.True(view.SqlServerPicker.CustomServerInput.Height > 0);
        Assert.True(
            view.SqlServerPicker.CustomServerInput.Bottom <=
            view.SqlServerPicker.ClientSize.Height);

        Assert.True(migrationView.SourceCard.Enabled);
        Assert.True(migrationView.DestinationCard.Enabled);
        Assert.Same(
            migrationView.SourceCard,
            migrationView.RoleColumns.GetControlFromPosition(0, 0));
        Assert.Same(
            migrationView.DestinationCard,
            migrationView.RoleColumns.GetControlFromPosition(1, 0));
        Assert.NotNull(migrationView.SourceSqlServerPicker);
        Assert.NotNull(migrationView.DestinationSqlServerPicker);
        Assert.False(migrationView.SourceEmbeddedStorageInput.Multiline);
        Assert.False(migrationView.DestinationEmbeddedStorageInput.Multiline);

        view.BackendSelector.SelectedIndex = 0;

        var testButton = FindButton(
            view,
            "Test readiness");

        Assert.NotNull(testButton);

    }

    [WinFormsFact]
    public void HivePersistenceSettingsAndMigrationFitNormalWorkspaceWithoutScrollOverflow()
    {
        var (management, _) =
            HiveWorkspaceLifecycleTests.ManagementFacadeProxy.Create();
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);
        var context = new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());

        using var settings = new HivePersistenceSettingsView(
            management,
            context,
            themeManager,
            "Hive.TestHost");
        settings.Size = new Size(1160, 760);
        settings.CreateControl();
        settings.PerformLayout();
        Application.DoEvents();

        var setupScrollHost =
            FindControl<HiveScrollHost>(
                settings.NavigationTabs.TabPages[0]);

        Assert.NotNull(setupScrollHost);
        setupScrollHost!.Synchronize();
        Assert.False(setupScrollHost.HorizontalScrollState.CanScroll);
        Assert.False(setupScrollHost.VerticalScrollState.CanScroll);

        using var migration = new HivePersistenceDataMigrationSettingsView(
            management,
            context,
            themeManager,
            "Hive.TestHost");
        migration.Size = new Size(1160, 760);
        migration.CreateControl();
        migration.PerformLayout();
        Application.DoEvents();

        var migrationScrollHost = FindControl<HiveScrollHost>(migration);

        Assert.NotNull(migrationScrollHost);
        migrationScrollHost!.Synchronize();
        Assert.False(migrationScrollHost.HorizontalScrollState.CanScroll);
        Assert.False(migrationScrollHost.VerticalScrollState.CanScroll);
    }

    [Fact]
    public void HiveSqlServerInstanceDiscovery_CombinesLocalAndNetworkInstances()
    {
        var instances = HiveSqlServerInstanceDiscovery.MergeCandidates(
        [
            @"REMOTE01\REPORTING",
            @"remote01\REPORTING"
        ],
        [
            "MSSQLSERVER",
            "HiveSql"
        ]);

        Assert.Equal(
            new[]
            {
                "localhost",
                @"localhost\HiveSql",
                @"REMOTE01\REPORTING"
            },
            instances);
    }

    [Fact]
    public void HiveSqlServerInstancePicker_DefaultsPortByInstanceKind()
    {
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);
        using var picker = new HiveSqlServerInstancePicker(themeManager);

        picker.SetDiscoveredInstances(
            new[] { @"localhost\HiveSql" },
            null);

        Assert.Equal(@"localhost\HiveSql", picker.ServerName);
        Assert.Null(picker.Port);

        picker.SetDiscoveredInstances(
            new[] { "localhost" },
            null);

        Assert.Equal("localhost", picker.ServerName);
        Assert.Equal(1433, picker.Port);

        picker.SetDiscoveredInstances(
            new[] { "localhost", @"localhost\HiveSql" },
            null);
        picker.ServerSelector.SelectedItem =
            picker.ServerSelector.Items
                .Cast<object>()
                .First(item => item.ToString() == @"localhost\HiveSql");

        Assert.Equal(@"localhost\HiveSql", picker.ServerName);
        Assert.Null(picker.Port);

        picker.ServerSelector.SelectedItem =
            picker.ServerSelector.Items[^1];

        Assert.True(picker.IsCustomSelected);
        Assert.Equal(1433, picker.Port);

        picker.CustomServerInput.Text = @"localhost\MSSQLSERVER01";
        Assert.Equal(@"localhost\MSSQLSERVER01", picker.ServerName);
        Assert.Null(picker.Port);

        picker.SetDiscoveredInstances(
            [
                "localhost",
                @"localhost\MSSQLSERVER01"
            ],
            @"localhost\MSSQLSERVER01",
            preserveCustomSelection: true);

        Assert.True(picker.IsCustomSelected);
        Assert.Equal(@"localhost\MSSQLSERVER01", picker.ServerName);
        Assert.Null(picker.Port);

        picker.SetDiscoveredInstances(
            [
                @"REMOTE01\SQL",
                @"localhost\HiveSql"
            ],
            null);

        Assert.Equal(
            @"localhost\HiveSql",
            picker.ServerName);
        Assert.Null(picker.Port);
    }

    [WinFormsFact]
    public async Task HiveSqlServerInstancePicker_PreservesCustomTextAndExplicitPortDuringDiscovery()
    {
        var networkStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseNetwork = new ManualResetEventSlim(false);
        var networkCalls = 0;
        var coordinator = new SqlServerInstanceDiscoveryCoordinator(
            () => new SqlServerInstanceDiscoveryInventory(new[] { "MSSQLSERVER" }),
            () =>
            {
                Interlocked.Increment(ref networkCalls);
                networkStarted.TrySetResult(true);
                releaseNetwork.Wait();
                return new SqlServerInstanceDiscoveryInventory(new[] { @"REMOTE01\REPORTING" });
            },
            networkWaitTimeout: TimeSpan.FromSeconds(10));

        var themeManager = new HiveThemeManager(HiveThemeMode.Light);
        using var host = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-2000, -2000),
            ClientSize = new Size(700, 180),
            ShowInTaskbar = false
        };
        using var picker = new HiveSqlServerInstancePicker(themeManager, coordinator) { Dock = DockStyle.Fill };
        host.Controls.Add(picker);
        host.Show();
        Application.DoEvents();

        picker.SetDiscoveredInstances(new[] { "localhost" }, "localhost");
        var refresh = picker.RefreshAsync("localhost", forceRefresh: true);
        Assert.True(picker.DiscoverySpinner.Visible);
        Assert.Equal("Searching for SQL Server instances…", picker.DiscoveryStatusLabel.Text);
        await networkStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Application.DoEvents();

        picker.ServerSelector.SelectedItem = picker.ServerSelector.Items[^1];
        picker.CustomServerInput.Text = @"manual-host\MyInstance";
        picker.PortInput.Text = "51433";
        Application.DoEvents();

        releaseNetwork.Set();
        await refresh.WaitAsync(TimeSpan.FromSeconds(5));
        Application.DoEvents();

        Assert.Equal(1, networkCalls);
        Assert.True(picker.IsCustomSelected);
        Assert.Equal(@"manual-host\MyInstance", picker.CustomServerInput.Text);
        Assert.Equal(51433, picker.Port);
        Assert.False(picker.DiscoverySpinner.Visible);
        Assert.NotEqual("Searching for SQL Server instances…", picker.DiscoveryStatusLabel.Text);
    }

    [WinFormsFact]
    public async Task HiveSqlServerInstancePicker_DisposalIgnoresLateDiscoveryCompletion()
    {
        var networkStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var networkFinished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseNetwork = new ManualResetEventSlim(false);
        var coordinator = new SqlServerInstanceDiscoveryCoordinator(
            () => new SqlServerInstanceDiscoveryInventory(new[] { "MSSQLSERVER" }),
            () =>
            {
                networkStarted.TrySetResult(true);
                releaseNetwork.Wait();
                networkFinished.TrySetResult(true);
                return new SqlServerInstanceDiscoveryInventory(new[] { @"REMOTE01\REPORTING" });
            },
            networkWaitTimeout: TimeSpan.FromSeconds(1),
            delay: static (_, _) => Task.CompletedTask);

        var themeManager = new HiveThemeManager(HiveThemeMode.Light);
        using var host = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-2000, -2000),
            ClientSize = new Size(700, 180),
            ShowInTaskbar = false
        };
        var picker = new HiveSqlServerInstancePicker(themeManager, coordinator) { Dock = DockStyle.Fill };
        host.Controls.Add(picker);
        host.Show();
        Application.DoEvents();

        var result = await picker.RefreshAsync(forceRefresh: true).WaitAsync(TimeSpan.FromSeconds(5));
        await networkStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(result.IsTimedOut);
        picker.Dispose();
        releaseNetwork.Set();
        await networkFinished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Application.DoEvents();
        Assert.True(picker.IsDisposed);
    }

    [WinFormsFact]
    public async Task HiveSqlServerInstancePicker_MergesNetworkResultsWhenTimedOutScanLaterCompletes()
    {
        var networkStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var networkFinished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseNetwork = new ManualResetEventSlim(false);
        var coordinator = new SqlServerInstanceDiscoveryCoordinator(
            () => new SqlServerInstanceDiscoveryInventory(new[] { "MSSQLSERVER" }),
            () =>
            {
                networkStarted.TrySetResult(true);
                releaseNetwork.Wait();
                networkFinished.TrySetResult(true);
                return new SqlServerInstanceDiscoveryInventory(new[] { @"REMOTE01\REPORTING" });
            },
            networkWaitTimeout: TimeSpan.FromSeconds(1),
            delay: static (_, _) => Task.CompletedTask);
        var completed = new TaskCompletionSource<SqlServerInstanceDiscoveryResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.NetworkResultsCompleted += result => completed.TrySetResult(result);

        var themeManager = new HiveThemeManager(HiveThemeMode.Light);
        using var host = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-2000, -2000),
            ClientSize = new Size(700, 180),
            ShowInTaskbar = false
        };
        using var picker = new HiveSqlServerInstancePicker(themeManager, coordinator) { Dock = DockStyle.Fill };
        host.Controls.Add(picker);
        host.Show();
        Application.DoEvents();

        var result = await picker.RefreshAsync(forceRefresh: true).WaitAsync(TimeSpan.FromSeconds(5));
        await networkStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        for (var attempt = 0; attempt < 50 &&
             !picker.DiscoveryStatusLabel.Text.Contains("timed out", StringComparison.OrdinalIgnoreCase); attempt++)
        {
            Application.DoEvents();
            await Task.Delay(10);
        }

        Assert.True(result.IsTimedOut);
        Assert.Contains("timed out", picker.DiscoveryStatusLabel.Text);

        releaseNetwork.Set();
        var lateResult = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await networkFinished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains(@"REMOTE01\REPORTING", lateResult.Instances);

        for (var attempt = 0; attempt < 50; attempt++)
        {
            Application.DoEvents();
            if (picker.ServerSelector.Items.Cast<object>()
                .Any(item => string.Equals(item.ToString(), @"REMOTE01\REPORTING", StringComparison.OrdinalIgnoreCase)))
                break;
            await Task.Delay(10);
        }

        Assert.Contains(
            @"REMOTE01\REPORTING",
            picker.ServerSelector.Items.Cast<object>().Select(item => item.ToString()));
        Assert.DoesNotContain("timed out", picker.DiscoveryStatusLabel.Text, StringComparison.OrdinalIgnoreCase);
    }

    [WinFormsFact]
    public void HiveSqlServerInstancePicker_ShowsInlineSearchingAndIncompleteStatus()
    {
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);
        using var host = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-2000, -2000),
            ClientSize = new Size(700, 180),
            ShowInTaskbar = false
        };
        using var picker = new HiveSqlServerInstancePicker(themeManager) { Dock = DockStyle.Fill };
        host.Controls.Add(picker);
        host.Show();
        Application.DoEvents();

        picker.SetDiscoveryPresentation(true, "Searching for SQL Server instances…");
        Assert.True(picker.DiscoverySpinner.Visible);
        Assert.Equal("Searching for SQL Server instances…", picker.DiscoveryStatusLabel.Text);

        picker.ServerSelector.SelectedItem = picker.ServerSelector.Items[^1];
        picker.CustomServerInput.Text = "manual-instance";
        Assert.True(picker.CustomServerInput.Visible);
        Assert.Equal("manual-instance", picker.CustomServerInput.Text);

        picker.SetDiscoveryPresentation(
            false,
            "Discovery timed out; results may be incomplete. Refresh or enter Custom...");
        Assert.False(picker.DiscoverySpinner.Visible);
        Assert.Contains("timed out", picker.DiscoveryStatusLabel.Text);
        Assert.True(picker.CustomServerInput.Visible);
        Assert.Equal("manual-instance", picker.CustomServerInput.Text);
    }

    [Fact]
    public void HiveSqlServerInstanceDiscovery_FormatsLocalInstancesForLocalConnection()
    {
        Assert.Equal(
            "localhost",
            HiveSqlServerInstanceDiscovery.FormatInstalledInstanceName(
                "MSSQLSERVER"));

        Assert.Equal(
            @"localhost\HiveSql",
            HiveSqlServerInstanceDiscovery.FormatInstalledInstanceName(
                "HiveSql"));
    }

    [WinFormsFact]
    public void HivePersistenceSettingsView_UsesSqlServerPickerCustomChoiceAndFooterStatus()
    {
        var (management, _) =
            HiveWorkspaceLifecycleTests.ManagementFacadeProxy.Create();
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);

        using var view = new HivePersistenceSettingsView(
            management,
            new ResourceAccessContext(
                DeploymentId.New(),
                TenantId.New(),
                PrincipalId.New()),
            themeManager,
            "Hive.TestHost");

        view.SqlServerPicker.SetDiscoveredInstances(
            new[] { @"localhost\MSSQLSERVER01", @"DEVBOX\SQLEXPRESS" },
            @"custom-host\HiveSql");

        Assert.Equal(
            "Custom...",
            view.SqlServerPicker.ServerSelector.Items[^1]?.ToString());
        Assert.True(view.SqlServerPicker.IsCustomSelected);
        Assert.Equal(
            @"custom-host\HiveSql",
            view.SqlServerPicker.ServerName);

        view.SqlServerPicker.SetValue(
            @"localhost\MSSQLSERVER01",
            1433);

        Assert.False(view.SqlServerPicker.IsCustomSelected);
        Assert.Equal(
            @"localhost\MSSQLSERVER01",
            view.SqlServerPicker.ServerName);
        Assert.Equal(1433, view.SqlServerPicker.Port);
        Assert.True(
            FindAncestor<FlowLayoutPanel>(view.StatusLabel)
                ?.Controls.Contains(view.StatusLabel) == true);
        Assert.NotNull(view.BrowseEmbeddedButton);
    }

    [WinFormsFact]
    public void HivePersistenceDataMigrationView_UsesTopDirectionAndScopeAndSqlAuthentication()
    {
        var (management, _) =
            HiveWorkspaceLifecycleTests.ManagementFacadeProxy.Create();
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);

        using var view = new HivePersistenceDataMigrationSettingsView(
            management,
            new ResourceAccessContext(
                DeploymentId.New(),
                TenantId.New(),
                PrincipalId.New()),
            themeManager,
            "Hive.TestHost");

        Assert.Equal(2, view.DirectionSelector.Items.Count);
        Assert.Equal("All Hive Data", view.ScopeLabel.Text);
        Assert.Equal(2, view.SqlAuthenticationSelector.Items.Count);
        Assert.True(view.FooterPanel.Controls.Contains(view.StatusLabel));

        Assert.Same(
            view.SourceCard,
            view.RoleColumns.GetControlFromPosition(0, 0));
        Assert.Same(
            view.DestinationCard,
            view.RoleColumns.GetControlFromPosition(1, 0));
    }

    [WinFormsFact]
    public async Task HivePersistenceDataMigration_UsesCompactEndpointLayoutAndNamedInstanceResolution()
    {
        var (management, managementProxy) =
            HiveWorkspaceLifecycleTests.ManagementFacadeProxy.Create();
        managementProxy.PersistenceConfiguration = new HivePersistenceConfiguration(
            HivePersistenceBackend.SqlServer,
            @"localhost\MSSQLSERVER01",
            null,
            "Hive-Hive.TestHost",
            HiveSqlAuthenticationMode.WindowsIntegrated,
            null,
            null,
            encrypt: true,
            trustServerCertificate: true,
            createDatabaseIfMissing: true,
            commandTimeoutSeconds: 37);
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);
        var context = new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());

        using var host = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-2000, -2000),
            ClientSize = new Size(1180, 760),
            ShowInTaskbar = false
        };
        using var view = new HivePersistenceDataMigrationSettingsView(
            management,
            context,
            themeManager,
            "Hive.TestHost")
        {
            Dock = DockStyle.Fill
        };

        host.Controls.Add(view);
        host.Show();
        Application.DoEvents();

        await view.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(6));

        Assert.Equal(
            managementProxy.PersistenceConfiguration.ServerName,
            view.SourceSqlServerPicker.ServerName);
        Assert.Equal(
            managementProxy.PersistenceConfiguration.DatabaseName,
            view.SourceDatabaseName);
        Assert.True(view.SourceEncrypt);
        Assert.True(view.SourceTrustServerCertificate);
        Assert.Null(view.SourceSqlServerPicker.Port);

        // The fixed top-row height must leave the complete custom ComboBox field
        // inside its parent rather than clipping its bottom edge.
        Assert.False(view.DirectionSelector.AutoSize);
        Assert.InRange(
            view.DirectionSelector.Height,
            view.DirectionSelector.PreferredSize.Height,
            view.DirectionSelector.PreferredSize.Height + 2);
        Assert.True(
            view.DirectionSelector.Bottom <= view.DirectionSelector.Parent!.ClientSize.Height);

        // A custom/named instance reveals additional picker rows. The endpoint
        // field and its stack must preserve the picker's auto-size behavior.
        view.SourceSqlServerPicker.SetDiscoveredInstances(
            Array.Empty<string>(),
            preferredServer: null);
        view.SourceSqlServerPicker.SetValue(
            @"localhost\MSSQLSERVER01",
            port: null);
        Application.DoEvents();

        Assert.True(view.SourceSqlServerPicker.AutoSize);
        Assert.True(view.SourceSqlServerPicker.CustomRow.Visible);
        Assert.True(view.SourceSqlServerPicker.Height > 36);
        Assert.True(
            view.SourceSqlServerPicker.Bottom <=
            view.SourceSqlServerPicker.Parent!.ClientSize.Height);
        Assert.True(
            view.SourceSqlServerPicker.Right <=
            view.SourceSqlServerPicker.Parent!.ClientSize.Width);

        var databaseAuthenticationRow = Assert.IsType<TableLayoutPanel>(
            view.SourceSqlServerLayout.GetControlFromPosition(0, 1));
        Assert.All(
            databaseAuthenticationRow.Controls.Cast<Control>(),
            control => Assert.True(
                control.Right <= databaseAuthenticationRow.ClientSize.Width,
                $"{control.GetType().Name} exceeds the SQL database/authentication row."));

        Assert.Equal(DockStyle.Top, view.SourceSqlServerLayout.Dock);
        Assert.NotEmpty(view.SourceSqlServerLayout.RowStyles.Cast<RowStyle>());
        Assert.All(
            view.SourceSqlServerLayout.RowStyles.Cast<RowStyle>(),
            static row => Assert.Equal(SizeType.AutoSize, row.SizeType));
        Assert.True(view.SourceSqlServerLayout.Height < view.SourceCard.Height - 100);

        Assert.Equal(DockStyle.Fill, view.DestinationEmbeddedStorageInput.Dock);
        Assert.True(view.DestinationEmbeddedStorageInput.AutoSize);
        Assert.True(view.DestinationEmbeddedStorageInput.Width >= 300);
        var embeddedPathPanel =
            Assert.IsType<TableLayoutPanel>(view.DestinationEmbeddedStorageInput.Parent);
        Assert.True(
            view.DestinationEmbeddedStorageInput.Width >=
            embeddedPathPanel.ClientSize.Width - 100);

        // Named instances resolve their own TCP port unless the user explicitly
        // entered one; migration must not force port 1433 onto this endpoint.
        view.SourceSqlServerPicker.SetValue(@"localhost\MSSQLSERVER01", port: null);

        var migrateButton = FindButton(view, "Migrate All Data");
        Assert.NotNull(migrateButton);
        migrateButton!.PerformClick();

        var testedConfiguration = await managementProxy.PersistenceConfigurationTested.Task
            .WaitAsync(TimeSpan.FromSeconds(5));
        WaitForUi(
            () => view.CopyDetailsButton.Visible,
            "A failed endpoint preflight did not expose Copy details.");

        Assert.Equal(@"localhost\MSSQLSERVER01", testedConfiguration.ServerName);
        Assert.Null(testedConfiguration.Port);
        Assert.Equal(
            managementProxy.PersistenceConfiguration.DatabaseName,
            testedConfiguration.DatabaseName);
        Assert.True(testedConfiguration.Encrypt);
        Assert.True(testedConfiguration.TrustServerCertificate);
        Assert.DoesNotContain(
            "intercepted the endpoint preflight",
            view.StatusLabel.Text,
            StringComparison.OrdinalIgnoreCase);
        var diagnosticDetails = Assert.IsType<string>(view.LastDiagnosticDetails);
        Assert.True(view.DiagnosticDetailsInput.Visible);
        Assert.True(view.DiagnosticDetailsInput.ReadOnly);
        Assert.Equal(diagnosticDetails, view.DiagnosticDetailsInput.Text);
        Assert.Contains(
            "intercepted the endpoint preflight",
            diagnosticDetails,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "named-instance port resolution",
            diagnosticDetails,
            StringComparison.OrdinalIgnoreCase);

        view.CopyDetailsButton.PerformClick();

        Assert.Equal(
            "Diagnostic details copied. Paste them into your message.",
            view.StatusLabel.Text);
        Assert.Equal(diagnosticDetails, Clipboard.GetText());
    }

    [Fact]
    public void HiveSettingsView_OpensOverviewByDefault()
    {
        var (management, _) =
            HiveWorkspaceLifecycleTests.ManagementFacadeProxy.Create();
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);
        using var view = new HiveSettingsView(
            management,
            new ResourceAccessContext(
                DeploymentId.New(),
                TenantId.New(),
                PrincipalId.New()),
            themeManager);

        var navigation = FindControl<HiveNavigationTree>(view);

        Assert.NotNull(navigation);
        Assert.NotNull(navigation!.SelectedNode);
        Assert.Equal("Overview", navigation.SelectedNode!.Text);
        Assert.Equal(
            "Navigate Hive package configuration by Overview, Providers, Agents, and Persistence.",
            navigation.AccessibleDescription);
        Assert.NotNull(FindLabel(view, "Configuration flow"));
    }

    [Fact]
    public async Task HiveSettingsView_RetriesCancelledPageInitializationAfterRapidNavigation()
    {
        var (management, proxy) = SettingsManagementProxy.Create();
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);

        using var host = new Form();
        using var view = new HiveSettingsView(
            management,
            new ResourceAccessContext(
                DeploymentId.New(),
                TenantId.New(),
                PrincipalId.New()),
            themeManager);

        host.Controls.Add(view);
        host.Show();
        Application.DoEvents();

        await view.InitializeAsync();

        var navigation = FindControl<HiveNavigationTree>(view);
        Assert.NotNull(navigation);

        var root = navigation!.Nodes[0];
        var overview = root.Nodes[0];
        var providerPage = root.Nodes[1];

        navigation.SelectedNode = providerPage;
        WaitForUi(
            () => proxy.InvocationCount == 1,
            "The first Settings page initialization did not reach management.");

        navigation.SelectedNode = overview;
        navigation.SelectedNode = providerPage;

        proxy.FirstCompletion.TrySetResult(
            Result<IReadOnlyList<Provider>>.Success(
                Array.Empty<Provider>()));

        WaitForUi(
            () => proxy.InvocationCount >= 2,
            "The cancelled Settings page initialization was not retried.");
    }

    [Fact]
    public void HiveNavigationTree_DoesNotSelectGroupNodes()
    {
        using var tree = new HiveNavigationTree();

        var group = new TreeNode("Group");
        var leaf = new TreeNode("Leaf");
        group.Nodes.Add(leaf);
        tree.Nodes.Add(group);
        tree.CreateControl();

        tree.SelectedNode = leaf;
        tree.SelectedNode = group;

        Assert.Same(leaf, tree.SelectedNode);
    }

    [Fact]
    public void HiveCrudPage_UsesNonAutosizingSearchAndCenteredStatusFilter()
    {
        using var page = new HiveCrudPage<TestItem>();

        Assert.False(page.SearchBox.AutoSize);

        page.StatusSelector = item => item.Name;

        var statusFilter = page.ActionBarPanel
            .Controls
            .OfType<TableLayoutPanel>()
            .SelectMany(static layout => layout.Controls.Cast<Control>())
            .OfType<FlowLayoutPanel>()
            .SelectMany(static flow => flow.Controls.Cast<Control>())
            .OfType<HiveComboBox>()
            .FirstOrDefault();

        Assert.NotNull(statusFilter);
        Assert.Equal(new Padding(0, 4, 0, 4), statusFilter!.Margin);
    }

    [Fact]
    public void HiveCrudPage_ReturnsStatusToNeutralAfterListRebuild()
    {
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);
        using var form = new TestHiveForm(themeManager);
        using var page = new HiveCrudPage<TestItem>();

        form.Body.Controls.Add(page);
        themeManager.Apply(form.Body);

        page.SetStatus("Operation failed.", HiveStatusTone.Error);
        page.SetColumns(
            new HiveCrudColumn<TestItem>(
                "Name",
                160,
                item => item.Name));

        Assert.Equal(
            themeManager.Theme.Palette.MutedText,
            page.StatusLabel.ForeColor);
    }

    [Fact]
    public async Task HiveCrudPageOperationController_ContainsCancellationCallbackExceptionsWhenSuperseding()
    {
        using var owner = new Panel();
        using var list = new ListView();
        using var searchBox = new TextBox();
        using var statusFilterBox = new HiveComboBox();
        using var pagination = new HivePaginationBar();
        using var controller = new HiveCrudPageOperationController(
            owner,
            list,
            searchBox,
            statusFilterBox,
            pagination,
            () => { },
            (_, _) => { },
            () => null,
            new object());

        var started = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var first = controller.ExecuteAsync(
            HiveCrudOperation.Load,
            async token =>
            {
                using var registration = token.Register(
                    static () => throw new InvalidOperationException(
                        "synthetic cancellation callback failure"));

                started.TrySetResult(true);
                await release.Task;
            },
            CancellationToken.None);

        await started.Task;

        var secondCompleted = false;
        var second = controller.ExecuteAsync(
            HiveCrudOperation.Load,
            _ =>
            {
                secondCompleted = true;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        await second;

        release.SetResult(true);
        await first;

        Assert.True(secondCompleted);
    }

    [Fact]
    public async Task HiveCrudPageOperationController_ContainsCancellationCallbackExceptionsDuringDispose()
    {
        using var owner = new Panel();
        using var list = new ListView();
        using var searchBox = new TextBox();
        using var statusFilterBox = new HiveComboBox();
        using var pagination = new HivePaginationBar();
        var controller = new HiveCrudPageOperationController(
            owner,
            list,
            searchBox,
            statusFilterBox,
            pagination,
            () => { },
            (_, _) => { },
            () => null,
            new object());

        var started = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var operation = controller.ExecuteAsync(
            HiveCrudOperation.Load,
            async token =>
            {
                using var registration = token.Register(
                    static () => throw new InvalidOperationException(
                        "synthetic cancellation callback failure"));

                started.TrySetResult(true);
                await release.Task;
            },
            CancellationToken.None);

        await started.Task;

        controller.Dispose();

        release.SetResult(true);
        await operation;
    }

    [Fact]
    public async Task HiveExampleTestSurface_ContainsCancellationCallbackExceptions()
    {
        using var surface = new HiveExampleTestSurface();
        var started = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var run = surface.RunAsync(async token =>
        {
            using var registration = token.Register(
                static () => throw new InvalidOperationException(
                    "synthetic cancellation callback failure"));

            started.TrySetResult(true);
            await Task.Delay(Timeout.Infinite, token);
        });

        await started.Task;

        surface.Cancel();
        await run;

        Assert.Equal("Cancelled.", surface.StatusLabel.Text);
    }

    [Fact]
    public async Task HiveCrudPage_DoesNotEscapeOperationFailureWithoutSubscriber()
    {
        using var page = new HiveCrudPage<TestItem>();

        page.LoadItemsAsync = _ =>
            Task.FromException<IReadOnlyList<TestItem>>(
                new InvalidOperationException("synthetic CRUD failure"));

        await page.RefreshAsync();

        Assert.Equal("Operation failed.", page.StatusLabel.Text);
    }

    [Fact]
    public async Task HiveCrudPage_DoesNotEscapeOperationFailureWhenSubscriberThrows()
    {
        using var page = new HiveCrudPage<TestItem>();

        page.LoadItemsAsync = _ =>
            Task.FromException<IReadOnlyList<TestItem>>(
                new InvalidOperationException("synthetic CRUD failure"));

        var secondSubscriberCalled = false;

        page.OperationFailed += (_, _) =>
            throw new InvalidOperationException("synthetic subscriber failure");

        page.OperationFailed += (_, _) =>
        {
            secondSubscriberCalled = true;
        };

        await page.RefreshAsync();

        Assert.Equal("Operation failed.", page.StatusLabel.Text);
        Assert.False(page.IsBusy);
        Assert.True(secondSubscriberCalled);
    }

    [Fact]
    public async Task HiveCrudPage_UsesFilterLanguageWhenFilteredResultIsEmpty()
    {
        using var page = new HiveCrudPage<TestItem>();

        page.SetColumns(
            new HiveCrudColumn<TestItem>(
                "Name",
                160,
                item => item.Name));

        page.LoadItemsAsync = _ =>
            Task.FromResult<IReadOnlyList<TestItem>>(
                new[]
                {
                    new TestItem("Alpha")
                });

        await page.RefreshAsync();

        page.SearchText = "missing";

        var emptyState = FindLabel(page, "No items match the current filters.");

        Assert.NotNull(emptyState);
        Assert.True(emptyState!.Visible);
    }

    [Fact]
    public async Task HiveCrudPage_DisposeDoesNotDisposeInFlightOperationCancellationSource()
    {
        var page = new HiveCrudPage<TestItem>();
        var started = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Exception? tokenUseFailure = null;

        page.LoadItemsAsync = async token =>
        {
            started.TrySetResult(true);
            await release.Task;

            try
            {
                using var registration = token.Register(static () => { });
            }
            catch (Exception exception)
            {
                tokenUseFailure = exception;
            }

            return
            [
                new TestItem("Alpha")
            ];
        };

        var refresh = page.RefreshAsync();
        await started.Task;

        page.Dispose();
        release.SetResult(true);

        await refresh;

        Assert.Null(tokenUseFailure);
    }

    [Fact]
    public async Task HiveExampleTestSurface_DisposeDoesNotDisposeInFlightRunCancellationSource()
    {
        var surface = new HiveExampleTestSurface();
        var started = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Exception? tokenUseFailure = null;

        var run = surface.RunAsync(async token =>
        {
            started.TrySetResult(true);
            await release.Task;

            try
            {
                using var registration = token.Register(static () => { });
            }
            catch (Exception exception)
            {
                tokenUseFailure = exception;
            }
        });

        await started.Task;

        surface.Dispose();
        release.SetResult(true);

        await run;

        Assert.Null(tokenUseFailure);
    }

    private static TControl? FindControl<TControl>(Control root)
        where TControl : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is TControl match)
                return match;

            var nested = FindControl<TControl>(child);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private static HiveButton? FindButton(
        Control root,
        string text)
    {
        foreach (Control child in root.Controls)
        {
            if (child is HiveButton button &&
                string.Equals(button.Text, text, StringComparison.Ordinal))
            {
                return button;
            }

            var nested = FindButton(child, text);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private static Label? FindLabel(Control root, string text)
    {
        foreach (Control child in root.Controls)
        {
            if (child is Label label &&
                string.Equals(label.Text, text, StringComparison.Ordinal))
            {
                return label;
            }

            var nested = FindLabel(child, text);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private static TControl? FindAncestor<TControl>(Control control)
        where TControl : Control
    {
        for (var current = control.Parent;
             current is not null;
             current = current.Parent)
        {
            if (current is TControl match)
                return match;
        }

        return null;
    }

    private static void WaitForUi(
        Func<bool> condition,
        string timeoutMessage)
    {
        var deadline = System.Diagnostics.Stopwatch.GetTimestamp() +
            (long)(System.Diagnostics.Stopwatch.Frequency * 5.0);

        while (!condition())
        {
            Application.DoEvents();

            if (System.Diagnostics.Stopwatch.GetTimestamp() >= deadline)
                throw new TimeoutException(timeoutMessage);

            Thread.Yield();
        }
    }

    private class SettingsManagementProxy : DispatchProxy
    {
        private readonly TaskCompletionSource<Result<IReadOnlyList<Provider>>> _firstCompletion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _invocationCount;

        public TaskCompletionSource<Result<IReadOnlyList<Provider>>> FirstCompletion =>
            _firstCompletion;

        public int InvocationCount =>
            Volatile.Read(ref _invocationCount);

        public static (
            IHiveManagementFacade Management,
            SettingsManagementProxy Proxy) Create()
        {
            var management =
                (IHiveManagementFacade)Create<IHiveManagementFacade, SettingsManagementProxy>();

            return (
                management,
                (SettingsManagementProxy)(object)management);
        }

        protected override object Invoke(
            MethodInfo? targetMethod,
            object?[]? args)
        {
            if (targetMethod?.Name == nameof(IHiveManagementFacade.ListProvidersAsync))
            {
                var invocation = Interlocked.Increment(ref _invocationCount);

                return invocation == 1
                    ? _firstCompletion.Task
                    : Task.FromResult(
                        Result<IReadOnlyList<Provider>>.Success(
                            Array.Empty<Provider>()));
            }

            throw new NotSupportedException(
                $"The Settings test proxy does not implement '{targetMethod?.Name}'.");
        }
    }

    [WinFormsFact]
    public void ExampleScrollablePages_UseHiveScrollHostWithoutNativeAutoScroll()
    {
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);

        using var overview = new OverviewExampleView(themeManager);
        using var dialogs = new DialogsExampleView(themeManager);
        using var crud = new ControlsCrudExampleView(themeManager);
        using var theme = new ThemeFoundationExampleView(themeManager);
        using var configuration = new ExampleConfigurationExampleView(themeManager);

        Assert.False(overview.AutoScroll);
        Assert.False(dialogs.AutoScroll);
        Assert.False(crud.AutoScroll);
        Assert.False(theme.AutoScroll);
        Assert.False(configuration.AutoScroll);

        Assert.IsType<HiveScrollHost>(Assert.Single(overview.Controls));
        Assert.IsType<HiveScrollHost>(Assert.Single(dialogs.Controls));
        Assert.IsType<HiveScrollHost>(Assert.Single(crud.Controls));
        Assert.IsType<HiveScrollHost>(Assert.Single(theme.Controls));
        Assert.IsType<HiveScrollHost>(Assert.Single(configuration.Controls));
    }

    [WinFormsFact]
    public void HiveSettingsOverview_UsesHiveScrollHost()
    {
        using var view = new HiveSettingsOverviewView();

        Assert.False(view.AutoScroll);
        var host = Assert.Single(view.Controls.OfType<HiveScrollHost>());
        Assert.NotNull(host.Content);
    }

    [WinFormsFact]
    public void HiveAdvancedOverview_UsesHiveScrollHost()
    {
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);
        using var view = new HiveAdvancedOverviewPage(themeManager);

        Assert.False(view.AutoScroll);
        var host = Assert.Single(view.Controls.OfType<HiveScrollHost>());
        Assert.NotNull(host.Content);
    }

    private sealed record TestItem(string Name);

    private sealed class TestHiveForm : HiveForm
    {
        public TestHiveForm(IHiveThemeManager themeManager)
            : base(
                "Test",
                string.Empty,
                new Size(720, 480),
                new Size(640, 420),
                themeManager)
        {
        }

        public Control Body => BodyPanel;
    }
}
