using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal sealed class ProviderCompletionIntegrationExample : IHiveExample
{
    public string Category => "Providers";

    public string Subcategory => "Runtime";

    public int Order => 21;

    public string Title => "Provider Completion Integration & Hardening";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return new ProviderCompletionIntegrationExampleView(
            services.GetExampleOutput());
    }
}
