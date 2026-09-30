using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal sealed class OverviewExample : IHiveExample
{
    public string Category => "Overview";

    public string Subcategory => "Getting Started";

    public int Order => 0;

    public string Title => "Project Overview";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var themeManager = services.GetThemeManager();

        return new OverviewExampleView(themeManager);
    }
}
