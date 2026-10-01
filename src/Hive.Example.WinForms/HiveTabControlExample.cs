namespace Hive.Example.WinForms;

internal sealed class HiveTabControlExample : IHiveExample
{
    public string Category => "UI";
    public string Subcategory => "Foundation";
    public int Order => 21;
    public string Title => "HiveTabControl";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new HiveTabControlExampleView(services);
    }
}
