using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal sealed class ProviderModelInformationExample : IHiveExample
{
    public string Category => "Providers";

    public string Subcategory => "Target Selection";

    public IReadOnlyList<string> AdditionalNavigationPath =>
        ["Capability Discovery", "Provider"];

    public int Order => 20;

    public string Title => "Model Information";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return new ProviderModelInformationExampleView(
            services.GetThemeManager(),
            services.GetExampleOutput());
    }
}
