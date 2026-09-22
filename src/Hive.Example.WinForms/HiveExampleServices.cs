using Hive.Host.WinForms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Example.WinForms;

internal sealed class HiveExampleServices : IServiceProvider
{
    private readonly IHiveThemeManager _themeManager;
    private readonly IHiveExampleOutput _output;
    private readonly IHiveManagementFacade _management;

    public HiveExampleServices(
        IHiveThemeManager themeManager,
        IHiveExampleOutput output,
        HiveHostServiceGraph graph)
    {
        ArgumentNullException.ThrowIfNull(themeManager);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(graph);

        _themeManager = themeManager;
        _output = output;
        _management = graph.Management;
    }

    public object? GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        return serviceType == typeof(IHiveThemeManager)
            ? _themeManager
            : serviceType == typeof(IHiveExampleOutput)
                ? _output
                : serviceType == typeof(IHiveManagementFacade)
                    ? _management
                    : null;
    }
}
