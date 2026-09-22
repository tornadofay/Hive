using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal sealed class OpenAICompatibleProviderExample : IHiveExample
{
    public string Category => "Providers";

    public string Subcategory => "Provider Transport";

    public int Order => 30;

    public string Title => "OpenAI-compatible Provider Adapter";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new OpenAICompatibleProviderExampleView(
            services.GetExampleOutput());
    }
}