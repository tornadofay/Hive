using System.Reflection;
using System.Windows.Forms;
using Hive.Core;
using Hive.Host.WinForms;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;
using Xunit;

namespace Hive.Tests;

public sealed class HiveWorkspaceLifecycleTests
{
    [Fact]
    public void Constructor_ExposesDirectLlmAndWorkItemsModulesWithoutStartingManagementCalls()
    {
        var (management, proxy) = ManagementFacadeProxy.Create();

        using var workspace = new HiveWorkspaceView(CreateContext(management));

        var navigation = FindControl<ListBox>(workspace);

        Assert.NotNull(navigation);
        Assert.Equal(
            ["Direct LLM", "Work Items"],
            navigation!.Items.Cast<object>().Select(item => item.ToString()));
        Assert.Equal(
            ["direct-llm", "work-items"],
            workspace.Modules.Select(module => module.Id));
        Assert.Null(workspace.ActiveModuleId);
        Assert.False(proxy.WorkItemsRequested.Task.IsCompleted);
    }

    [Fact]
    public void ShowModule_WorkItems_ShowsExplicitActivitySelectionStateWithoutManagementCalls()
    {
        var (management, proxy) = ManagementFacadeProxy.Create();

        using var workspace = new HiveWorkspaceView(CreateContext(management));

        workspace.ShowModule("work-items");

        var state = FindLabel(
            workspace,
            "Select a WorkItem to view activity.");

        Assert.Equal("work-items", workspace.ActiveModuleId);
        Assert.NotNull(state);
        Assert.True(state!.Visible);
        Assert.False(proxy.WorkItemsRequested.Task.IsCompleted);
    }

    [Fact]
    public void ShowModule_DirectLlm_DoesNotCallManagementUntilRefreshed()
    {
        var (management, _) = ManagementFacadeProxy.Create();

        using var workspace = new HiveWorkspaceView(CreateContext(management));

        workspace.ShowModule("direct-llm");

        Assert.Equal("direct-llm", workspace.ActiveModuleId);
    }

    [Fact]
    public void RefreshAsync_DoesNotMutateDisposedViewWhenManagementCompletesLate()
    {
        var (management, proxy) = ManagementFacadeProxy.Create();

        using var workspace = new HiveWorkspaceView(CreateContext(management));

        workspace.ShowModule("work-items");
        var refresh = workspace.RefreshAsync();

        WaitForTask(
            proxy.WorkItemsRequested.Task,
            "The workspace did not reach the management facade.");

        workspace.Dispose();

        proxy.WorkItemsCompletion.TrySetResult(
            Result<WorkItemListPage>.Success(
                new WorkItemListPage(
                    Array.Empty<WorkItem>(),
                    nextCursor: null)));

        WaitForTask(
            refresh,
            "The disposed workspace refresh did not complete.");
    }

    [Fact]
    public void Dispose_IsSafeToCallMoreThanOnceWithLoadedModules()
    {
        var (management, _) = ManagementFacadeProxy.Create();
        var workspace = new HiveWorkspaceView(CreateContext(management));

        workspace.ShowModule("direct-llm");
        workspace.ShowModule("work-items");

        workspace.Dispose();
        workspace.Dispose();
    }

    [Fact]
    public async Task ActivateModuleAsync_LoadsAModuleOnlyTheFirstTimeItIsShown()
    {
        var (management, _) = ManagementFacadeProxy.Create();
        var first = new FakeModule("first", 10);
        var second = new FakeModule("second", 20);

        using var workspace = new HiveWorkspaceView(
            CreateContext(management),
            [first, second]);

        await workspace.ActivateModuleAsync("first");
        await workspace.ActivateModuleAsync("second");
        await workspace.ActivateModuleAsync("first");

        Assert.Equal(1, first.RefreshCount);
        Assert.Equal(1, second.RefreshCount);
        Assert.Equal("first", workspace.ActiveModuleId);
    }

    [Fact]
    public async Task ManagementChanged_RefreshesOnlyTheActiveModule()
    {
        var (management, _) = ManagementFacadeProxy.Create();
        var first = new FakeModule("first", 10);
        var second = new FakeModule("second", 20);
        var context = CreateContext(management);

        using var workspace = new HiveWorkspaceView(context, [first, second]);

        await workspace.ActivateModuleAsync("first");
        await workspace.ActivateModuleAsync("second");
        context.NotifyManagementChanged();

        Assert.Equal(1, first.RefreshCount);
        Assert.Equal(2, second.RefreshCount);
    }

    [Fact]
    public void Constructor_RejectsDuplicateOrEmptyModuleSets()
    {
        var (management, _) = ManagementFacadeProxy.Create();
        var context = CreateContext(management);

        Assert.Throws<ArgumentException>(
            () => new HiveWorkspaceView(
                context,
                [new FakeModule("same", 1), new FakeModule("same", 2)]));

        Assert.Throws<ArgumentException>(
            () => new HiveWorkspaceView(
                context,
                Array.Empty<IHiveWorkspaceModule>()));
    }

    [Fact]
    public void Context_ResolvesTheCurrentManagementFacadeOnEveryAccess()
    {
        var (first, _) = ManagementFacadeProxy.Create();
        var (second, _) = ManagementFacadeProxy.Create();
        var current = first;

        var context = new HiveWorkspaceContext(
            () => current,
            CreateAccessContext(),
            new HiveThemeManager(HiveThemeMode.Light));

        Assert.Same(first, context.Management);

        current = second;

        Assert.Same(second, context.Management);
    }

    private static HiveWorkspaceContext CreateContext(
        IHiveManagementFacade management) =>
        new(
            management,
            CreateAccessContext(),
            new HiveThemeManager(HiveThemeMode.Light));

    private static ResourceAccessContext CreateAccessContext() =>
        new(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());

    private static Label? FindLabel(
        Control root,
        string text)
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

    private static TControl? FindControl<TControl>(
        Control root)
        where TControl : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is TControl target)
                return target;

            var nested = FindControl<TControl>(child);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private static void WaitForTask(
        Task task,
        string timeoutMessage)
    {
        var deadline = System.Diagnostics.Stopwatch.GetTimestamp() +
            (long)(System.Diagnostics.Stopwatch.Frequency * 5.0);

        while (!task.IsCompleted)
        {
            Application.DoEvents();

            if (System.Diagnostics.Stopwatch.GetTimestamp() >= deadline)
                throw new TimeoutException(timeoutMessage);

            Thread.Yield();
        }

        task.GetAwaiter().GetResult();
    }

    private sealed class FakeModule : IHiveWorkspaceModule
    {
        public FakeModule(string id, int order)
        {
            Id = id;
            Order = order;
        }

        public string Id { get; }

        public string Title => Id;

        public int Order { get; }

        public FakeView? View { get; private set; }

        public int RefreshCount => View?.RefreshCount ?? 0;

        public Control CreateView(HiveWorkspaceContext context) =>
            View = new FakeView();
    }

    private sealed class FakeView : Control, IHiveWorkspaceRefreshable
    {
        public int RefreshCount { get; private set; }

        public Task RefreshAsync(CancellationToken cancellationToken)
        {
            RefreshCount++;
            return Task.CompletedTask;
        }
    }

    public class ManagementFacadeProxy : DispatchProxy
    {
        private readonly TaskCompletionSource<bool> _workItemsRequested =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<Result<WorkItemListPage>> _workItemsCompletion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _persistenceConnectionTestRequested =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<HivePersistenceConfiguration> _persistenceConfigurationTested =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _persistenceMigrationRequested =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public HivePersistenceConnectionTest? PersistenceConnectionTestResult { get; set; }
        public Result<HivePersistenceMigrationResult>? PersistenceMigrationResult { get; set; }

        public HivePersistenceConfiguration PersistenceConfiguration { get; set; } =
            new(
                HivePersistenceBackend.SqlServer,
                "localhost",
                1433,
                "Hive-Hive.TestHost",
                HiveSqlAuthenticationMode.WindowsIntegrated,
                null,
                null,
                encrypt: true,
                trustServerCertificate: true,
                createDatabaseIfMissing: true,
                commandTimeoutSeconds: 37);

        public TaskCompletionSource<bool> PersistenceConnectionTestRequested =>
            _persistenceConnectionTestRequested;

        public TaskCompletionSource<HivePersistenceConfiguration> PersistenceConfigurationTested =>
            _persistenceConfigurationTested;

        public TaskCompletionSource<bool> PersistenceMigrationRequested =>
            _persistenceMigrationRequested;

        public TaskCompletionSource<bool> WorkItemsRequested =>
            _workItemsRequested;

        public TaskCompletionSource<Result<WorkItemListPage>> WorkItemsCompletion =>
            _workItemsCompletion;

        public static (
            IHiveManagementFacade Management,
            ManagementFacadeProxy Proxy) Create()
        {
            var management =
                (IHiveManagementFacade)Create<IHiveManagementFacade, ManagementFacadeProxy>();

            return (management, (ManagementFacadeProxy)(object)management);
        }

        protected override object Invoke(
            MethodInfo? targetMethod,
            object?[]? args)
        {
            if (targetMethod?.Name == nameof(IHiveManagementFacade.ListWorkItemsPageAsync))
            {
                _workItemsRequested.TrySetResult(true);
                return _workItemsCompletion.Task;
            }

            if (targetMethod?.Name == nameof(IHiveManagementFacade.GetPersistenceConfigurationAsync))
            {
                return Task.FromResult(
                    Result<HivePersistenceConfiguration>.Success(PersistenceConfiguration));
            }

            if (targetMethod?.Name == nameof(IHiveManagementFacade.TestPersistenceConnectionAsync))
            {
                _persistenceConnectionTestRequested.TrySetResult(true);
                _persistenceConfigurationTested.TrySetResult(
                    (HivePersistenceConfiguration)args![0]!);

                if (PersistenceConnectionTestResult is { } configuredResult)
                {
                    return Task.FromResult(
                        Result<HivePersistenceConnectionTest>.Success(configuredResult));
                }

                return Task.FromResult(
                    Result<HivePersistenceConnectionTest>.Failure(
                        Error.Validation(
                            "hive.tests.persistence-connection-intercepted",
                            "The test proxy intercepted the endpoint preflight.")));
            }

            if (targetMethod?.Name == nameof(IHiveManagementFacade.MigratePersistenceDataAsync))
            {
                _persistenceMigrationRequested.TrySetResult(true);
                return Task.FromResult(
                    PersistenceMigrationResult ??
                    Result<HivePersistenceMigrationResult>.Failure(
                        Error.Conflict(
                            "hive.tests.persistence-migration-not-configured",
                            "The test proxy did not configure a migration result.")));
            }

            throw new NotSupportedException(
                $"The test proxy does not implement '{targetMethod?.Name}'.");
        }
    }
}
