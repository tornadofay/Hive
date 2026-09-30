using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed class ThemeFoundationExample : IHiveExample
{
    public string Category => "UI";

    public string Subcategory => "Foundation";

    public int Order => 10;

    public string Title => "Theme";

    public UserControl CreateView(IServiceProvider services)
    {
        var themeManager = services.GetThemeManager();

        return new ThemeFoundationExampleView(themeManager);
    }
}
