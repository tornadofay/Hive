using System.Drawing;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Hive.Core;
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
            "Navigate Hive package configuration by Overview, Providers, Accounts / Credentials, Execution Targets, Agents, and Persistence.",
            navigation.AccessibleDescription);
        Assert.NotNull(FindLabel(view, "Configuration flow"));
    }

    [Fact]
    public void HiveSettingsView_RetriesCancelledPageInitializationAfterRapidNavigation()
    {
        var (management, proxy) = SettingsManagementProxy.Create();
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

        var root = navigation!.Nodes[0];
        var overview = root.Nodes[0];
        var providerPage = root.Nodes[1].Nodes[0];

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
            .OfType<ComboBox>()
            .FirstOrDefault();

        Assert.NotNull(statusFilter);
        Assert.Equal(new Padding(0, 4, 0, 4), statusFilter!.Margin);
        Assert.False(statusFilter.IntegralHeight);
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

    private static void WaitForUi(
        Func<bool> condition,
        string timeoutMessage)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (!condition())
        {
            Application.DoEvents();

            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException(timeoutMessage);

            Thread.Sleep(10);
        }
    }

    private sealed class SettingsManagementProxy : DispatchProxy
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
