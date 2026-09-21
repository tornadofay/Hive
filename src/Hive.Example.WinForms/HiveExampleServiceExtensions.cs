using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal static class HiveExampleServiceExtensions
{
    public static IHiveThemeManager GetThemeManager(
        this IServiceProvider services) =>
        GetRequired<IHiveThemeManager>(services);

    public static IHiveExampleOutput GetExampleOutput(
        this IServiceProvider services) =>
        GetRequired<IHiveExampleOutput>(services);

    private static T GetRequired<T>(IServiceProvider services)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.GetService(typeof(T)) as T
            ?? throw new InvalidOperationException(
                $"Hive Example service '{typeof(T).Name}' is not available.");
    }
}
