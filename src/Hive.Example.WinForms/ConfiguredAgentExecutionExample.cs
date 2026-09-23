using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal sealed class ConfiguredAgentExecutionExample : IHiveExample
{
    public string Category => "Agents";

    public string Subcategory => "Base Agent";

    public int Order => 31;

    public string Title => "Configured Agent Execution";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return new ConfiguredAgentExecutionExampleView(
            services.GetHiveExampleServices());
    }
}
