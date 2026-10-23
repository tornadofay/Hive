using System.Windows.Forms;
using Hive.Core;
using Hive.Host.WinForms;
using Hive.Host.WinForms.UI.Theme;
using Xunit;

namespace Hive.Tests;

public sealed class HiveWorkspaceLauncherTests
{
    [WinFormsFact]
    public void Show_ReusesTheOpenWindowAndCreatesANewOneAfterItCloses()
    {
        var (management, _) = HiveWorkspaceLifecycleTests.ManagementFacadeProxy.Create();
        var context = new HiveWorkspaceContext(
            management,
            new ResourceAccessContext(
                DeploymentId.New(),
                TenantId.New(),
                PrincipalId.New()),
            new HiveThemeManager(HiveThemeMode.Light));

        using var launcher = new HiveWorkspaceLauncher(
            context,
            [new QuietModule()]);

        Assert.False(launcher.IsOpen);

        var first = launcher.Show();

        Assert.True(launcher.IsOpen);
        Assert.Same(first, launcher.Show());

        first.Close();

        Assert.False(launcher.IsOpen);

        var second = launcher.Show();

        Assert.NotSame(first, second);
        Assert.True(launcher.IsOpen);

        second.Close();

        Assert.False(launcher.IsOpen);
    }

    private sealed class QuietModule : IHiveWorkspaceModule
    {
        public string Id => "quiet";

        public string Title => "Quiet";

        public int Order => 1;

        public Control CreateView(HiveWorkspaceContext context) => new Panel();
    }
}
