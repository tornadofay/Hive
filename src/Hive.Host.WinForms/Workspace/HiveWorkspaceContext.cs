using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Host.WinForms;

/// <summary>
/// Services shared by the Workspace shell and its modules. The Management facade is resolved on every
/// access so a long-lived Workspace window never calls a facade that the host has retired.
/// </summary>
public sealed class HiveWorkspaceContext
{
    private readonly Func<IHiveManagementFacade> _managementAccessor;

    public HiveWorkspaceContext(
        IHiveManagementFacade management,
        ResourceAccessContext accessContext,
        IHiveThemeManager themeManager,
        IHiveExampleOutput? output = null)
        : this(CreateAccessor(management), accessContext, themeManager, output)
    {
    }

    public HiveWorkspaceContext(
        Func<IHiveManagementFacade> managementAccessor,
        ResourceAccessContext accessContext,
        IHiveThemeManager themeManager,
        IHiveExampleOutput? output = null)
    {
        _managementAccessor = managementAccessor
            ?? throw new ArgumentNullException(nameof(managementAccessor));
        AccessContext = accessContext
            ?? throw new ArgumentNullException(nameof(accessContext));
        ThemeManager = themeManager
            ?? throw new ArgumentNullException(nameof(themeManager));
        Output = output;
    }

    /// <summary>Raised after the host replaced its Management graph. Modules reload their data.</summary>
    public event EventHandler? ManagementChanged;

    public IHiveManagementFacade Management => _managementAccessor();

    public ResourceAccessContext AccessContext { get; }

    public IHiveThemeManager ThemeManager { get; }

    public IHiveExampleOutput? Output { get; }

    public void NotifyManagementChanged() =>
        ManagementChanged?.Invoke(this, EventArgs.Empty);

    private static Func<IHiveManagementFacade> CreateAccessor(
        IHiveManagementFacade management)
    {
        ArgumentNullException.ThrowIfNull(management);
        return () => management;
    }
}
