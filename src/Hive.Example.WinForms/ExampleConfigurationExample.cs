using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal sealed class ExampleConfigurationExample : IHiveExample
{
    public string Category => "Overview";

    public string Subcategory => "Getting Started";

    public int Order => 1;

    public string Title => "Example Configuration";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return new ExampleConfigurationExampleView(
            services.GetThemeManager());
    }
}
