using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal sealed class WorkspaceExample : IHiveExample
{
    public string Category => "Overview";

    public string Subcategory => "Getting Started";

    public int Order => 2;

    public string Title => "Workspace";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return new WorkspaceExampleView(services);
    }
}
