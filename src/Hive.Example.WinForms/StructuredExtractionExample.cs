using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal sealed class StructuredExtractionExample : IHiveExample
{
    public string Category => "Workspace";

    public string Subcategory => "WorkItem Operations";

    public IReadOnlyList<string> AdditionalNavigationPath =>
        ["Structured Extraction & Validation"];

    public int Order => 21;

    public string Title => "Phase 1.17 Structured Extraction & Validation";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return new StructuredExtractionExampleView(
            services.GetThemeManager(),
            services.GetExampleOutput());
    }
}
