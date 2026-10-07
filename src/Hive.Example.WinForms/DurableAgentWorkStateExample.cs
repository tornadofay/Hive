using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal sealed class DurableAgentWorkStateExample : IHiveExample
{
    public string Category => "Persistence";

    public string Subcategory => "Agent Work State";

    public int Order => 40;

    public string Title => "Durable Base-Agent Work State & Runtime Recovery";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return new DurableAgentWorkStateExampleView(
            services.GetExampleOutput());
    }
}
