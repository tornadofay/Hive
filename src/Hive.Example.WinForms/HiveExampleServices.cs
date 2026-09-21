using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed class HiveExampleServices : IServiceProvider
{
    private readonly IHiveThemeManager _themeManager;
    private readonly IHiveExampleOutput _output;

    public HiveExampleServices(
        IHiveThemeManager themeManager,
        IHiveExampleOutput output)
    {
        ArgumentNullException.ThrowIfNull(themeManager);
        ArgumentNullException.ThrowIfNull(output);
        _themeManager = themeManager;
        _output = output;
    }

    public object? GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        return serviceType == typeof(IHiveThemeManager)
            ? _themeManager
            : serviceType == typeof(IHiveExampleOutput)
                ? _output
                : null;
    }
}
