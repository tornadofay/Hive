using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal sealed class ControlsCrudExample : IHiveExample
{
    public string Category => "UI";

    public string Subcategory => "Foundation";

    public int Order => 20;

    public string Title => "Controls & CRUD";

    public UserControl CreateView(IServiceProvider services)
    {
        var themeManager = services.GetThemeManager();

        return new ControlsCrudExampleView(themeManager);
    }
}
