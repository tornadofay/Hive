using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed class ControlsCrudExample : IHiveExample
{
    public string Category => "UI";

    public string Subcategory => "Foundation";

    public int Order => 20;

    public string Title => "Controls & CRUD";

    public UserControl CreateView(IServiceProvider services)
    {
        var themeManager = services.GetService(typeof(IHiveThemeManager))
            as IHiveThemeManager
            ?? throw new InvalidOperationException(
                "The Example host did not provide IHiveThemeManager.");

        return new ControlsCrudExampleView(themeManager);
    }
}
