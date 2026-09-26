using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal sealed class FirstRealAgentExecutionExample : IHiveExample
{
    public string Category => "Agents";

    public string Subcategory => "Base Agent";

    public int Order => 30;

    public string Title => "First Real Agent Execution";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return new FirstRealAgentExecutionExampleView(
            services.GetExampleOutput());
    }
}
