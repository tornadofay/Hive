using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal sealed class ExecutionTargetSelectionExample : IHiveExample
{
    public string Category => "Providers";

    public string Subcategory => "Target Selection";

    public int Order => 20;

    public string Title => "Capability-aware Execution Target Selection";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return new ExecutionTargetSelectionExampleView(
            services.GetExampleOutput());
    }
}