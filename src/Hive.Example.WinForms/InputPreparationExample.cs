using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal sealed class InputPreparationExample : IHiveExample
{
    public string Category => "Workspace";
    public string Subcategory => "WorkItem Operations";
    public IReadOnlyList<string> AdditionalNavigationPath => ["Input Preparation & Routing"];
    public int Order => 20;
    public string Title => "V1 Input Preparation & Routing";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new InputPreparationExampleView(
            services.GetManagementFacade(),
            services.GetExampleAccessContext(),
            services.GetThemeManager(),
            services.GetExampleOutput());
    }
}
