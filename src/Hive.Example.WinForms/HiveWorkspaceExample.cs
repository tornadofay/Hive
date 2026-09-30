using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal sealed class HiveWorkspaceExample : IHiveExample
{
    public string Category => "Workspace";

    public string Subcategory => "WorkItem Operations";

    public int Order => 10;

    public string Title => "V1 Workspace & WorkItem Operations";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new HiveWorkspaceExampleView(services);
    }
}
