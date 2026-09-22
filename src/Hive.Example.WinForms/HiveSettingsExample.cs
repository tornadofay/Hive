namespace Hive.Example.WinForms;

internal sealed class HiveSettingsExample : IHiveExample
{
    public string Category => "Settings";

    public string Subcategory => "Configuration";

    public int Order => 10;

    public string Title => "Hive Settings / Provider & Persistence";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new HiveSettingsExampleView(services);
    }
}
