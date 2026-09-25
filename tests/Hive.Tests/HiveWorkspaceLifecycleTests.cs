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
    public async Task RefreshAsync_DoesNotMutateDisposedViewWhenManagementCompletesLate()
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

        await proxy.WorkItemsRequested.Task;
        workspace.Dispose();

        proxy.WorkItemsRequested.TrySetResult(
            Result<IReadOnlyList<WorkItem>>.Success(
                Array.Empty<WorkItem>()));

        await refresh;
    }

    public sealed class ManagementFacadeProxy : DispatchProxy
    {
        private readonly TaskCompletionSource<Result<IReadOnlyList<WorkItem>>> _workItemsRequested =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<Result<IReadOnlyList<WorkItem>>> WorkItemsRequested =>
            _workItemsRequested;

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
                return _workItemsRequested.Task;

            throw new NotSupportedException(
                $"The test proxy does not implement '{targetMethod?.Name}'.");
        }
    }
}
