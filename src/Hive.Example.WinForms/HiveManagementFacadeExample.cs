using Hive.Host.WinForms.UI.Controls;

namespace Hive.Example.WinForms;

internal sealed class HiveManagementFacadeExample : IHiveExample
{
    public string Category => "Management";

    public string Subcategory => "Facade";

    public IReadOnlyList<string> AdditionalNavigationPath =>
        Array.Empty<string>();

    public int Order => 10;

    public string Title => "Hive.Management CRUD Facade";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new HiveManagementFacadeExampleView(services);
    }
}
