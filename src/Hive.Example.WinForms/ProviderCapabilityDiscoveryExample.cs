using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal sealed class ProviderCapabilityDiscoveryExample : IHiveExample
{
    public string Category => "Providers";

    public string Subcategory => "Target Selection";

    public IReadOnlyList<string> AdditionalNavigationPath =>
        ["Capability Discovery"];

    public int Order => 10;

    public string Title => "Provider / Model Capability Discovery";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return new ProviderCapabilityDiscoveryExampleView(
            services.GetManagementFacade(),
            services.GetExampleOutput());
    }
}
