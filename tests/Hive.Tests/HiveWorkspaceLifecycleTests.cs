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
    public void Constructor_ShowsExplicitActivitySelectionState()
    {
        var (management, _) = ManagementFacadeProxy.Create();
        var accessContext = new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);

        using var workspace = new HiveWorkspaceView(
            management,
            accessContext,
            themeManager);

        var state = FindLabel(
            workspace,
            "Select a WorkItem to view activity.");

        Assert.NotNull(state);
        Assert.True(state!.Visible);
    }

    [Fact]
    public void RefreshAsync_DoesNotMutateDisposedViewWhenManagementCompletesLate()
    {
        var (management, proxy) = ManagementFacadeProxy.Create();
        var accessContext = new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);

        using var workspace = new HiveWorkspaceView(
            management,
            accessContext,
            themeManager);

        var refresh = workspace.RefreshAsync();

        WaitForTask(
            proxy.WorkItemsRequested.Task,
            "The workspace did not reach the management facade.");

        workspace.Dispose();

        proxy.WorkItemsCompletion.TrySetResult(
            Result<IReadOnlyList<WorkItem>>.Success(
                Array.Empty<WorkItem>()));

        WaitForTask(
            refresh,
            "The disposed workspace refresh did not complete.");
    }

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

    private static void WaitForTask(
        Task task,
        string timeoutMessage)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (!task.IsCompleted)
        {
            Application.DoEvents();

            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException(timeoutMessage);

            Thread.Sleep(10);
        }

        task.GetAwaiter().GetResult();
    }

    public class ManagementFacadeProxy : DispatchProxy
    {
        private readonly TaskCompletionSource<bool> _workItemsRequested =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<Result<IReadOnlyList<WorkItem>>> _workItemsCompletion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> WorkItemsRequested =>
            _workItemsRequested;

        public TaskCompletionSource<Result<IReadOnlyList<WorkItem>>> WorkItemsCompletion =>
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
            if (targetMethod?.Name == nameof(IHiveManagementFacade.ListWorkItemsAsync))
            {
                _workItemsRequested.TrySetResult(true);
                return _workItemsCompletion.Task;
            }

            throw new NotSupportedException(
                $"The test proxy does not implement '{targetMethod?.Name}'.");
        }
    }
}
