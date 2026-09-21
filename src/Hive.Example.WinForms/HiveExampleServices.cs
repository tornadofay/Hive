using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed class HiveExampleServices : IServiceProvider
{
    private readonly IHiveThemeManager _themeManager;

    public HiveExampleServices(IHiveThemeManager themeManager)
    {
        ArgumentNullException.ThrowIfNull(themeManager);
        _themeManager = themeManager;
    }

    public object? GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        return serviceType == typeof(IHiveThemeManager)
            ? _themeManager
            : null;
    }
}
