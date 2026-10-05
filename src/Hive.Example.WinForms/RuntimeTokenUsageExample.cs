using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal sealed class RuntimeTokenUsageExample : IHiveExample
{
    public string Category => "Providers";

    public string Subcategory => "Runtime";

    public int Order => 20;

    public string Title => "Token Usage Foundation";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return new RuntimeTokenUsageExampleView(
            services.GetExampleOutput());
    }
}
