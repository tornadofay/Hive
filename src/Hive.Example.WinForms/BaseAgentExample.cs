using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal sealed class BaseAgentExample : IHiveExample
{
    public string Category => "Agents";

    public string Subcategory => "Base Agent";

    public int Order => 10;

    public string Title => "AgentFactory / Runtime Isolation";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return new BaseAgentExampleView(
            services.GetExampleOutput());
    }
}