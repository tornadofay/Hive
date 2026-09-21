using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed class ExampleGenericReusableUiExample : IHiveExample
{
    public string Category => "UI";

    public string Subcategory => "Foundation";

    public int Order => 40;

    public string Title => "Example generic reusable UI";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return new ExampleGenericReusableUiExampleView(services);
    }
}
