using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal sealed class DualBusinessAppIntegrationExample : IHiveExample
{
    public string Category => "Host";

    public string Subcategory => "WinForms Integration";

    public int Order => 20;

    public string Title => "Dual Business-App Integration Contract";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new DualBusinessAppIntegrationExampleView(
            services.GetThemeManager(),
            services.GetExampleOutput(),
            services);
    }
}
