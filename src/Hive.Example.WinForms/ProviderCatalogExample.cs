using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal sealed class ProviderCatalogExample : IHiveExample
{
    public string Category => "Providers";

    public string Subcategory => "Provider Platform";

    public int Order => 20;

    public string Title => "Built-In Provider Catalog";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return new ProviderCatalogExampleView(
            services.GetThemeManager(),
            services.GetExampleOutput());
    }
}
