using Hive.Core;
using Hive.Host.WinForms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Example.WinForms;

/// <summary>
/// Owns the Example Host's Workspace window through the public launcher. The Management accessor is
/// resolved on every call, and the host notifies the Workspace after it replaces its Management graph.
/// </summary>
internal sealed class HiveExampleWorkspaceHost : IDisposable
{
    private readonly HiveWorkspaceContext _context;
    private readonly HiveWorkspaceLauncher _launcher;

    public HiveExampleWorkspaceHost(
        Func<IHiveManagementFacade> managementAccessor,
        ResourceAccessContext accessContext,
        IHiveThemeManager themeManager,
        IHiveExampleOutput output)
    {
        _context = new HiveWorkspaceContext(
            managementAccessor,
            accessContext,
            themeManager,
            output);
        _launcher = new HiveWorkspaceLauncher(_context);
    }

    public void Open(IWin32Window owner) =>
        _launcher.Show(owner);

    public void NotifyManagementChanged() =>
        _context.NotifyManagementChanged();

    public void Dispose() =>
        _launcher.Dispose();
}
