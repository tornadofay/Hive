using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal sealed class WinFormsHostContextExample : IHiveExample
{
    public string Category => "Host";

    public string Subcategory => "WinForms Integration";

    public int Order => 10;

    public string Title => "Image Input & WinForms Host Context";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return new WinFormsHostContextExampleView(
            services.GetThemeManager(),
            services.GetExampleOutput());
    }
}
