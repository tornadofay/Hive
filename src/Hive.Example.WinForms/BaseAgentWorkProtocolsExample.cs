using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal sealed class BaseAgentWorkProtocolsExample : IHiveExample
{
    public string Category => "Agents";

    public string Subcategory => "Base Agent";

    public int Order => 20;

    public string Title => "Base Agent Work Protocols";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return new BaseAgentWorkProtocolsExampleView(
            services.GetExampleOutput());
    }
}
