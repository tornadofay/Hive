using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed class ScrollInfrastructureExample : IHiveExample
{
    public string Category => "UI";

    public string Subcategory => "Foundation";

    public int Order => 15;

    public string Title => "Scroll Infrastructure";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return new ScrollInfrastructureExampleView(
            services.GetThemeManager());
    }
}
