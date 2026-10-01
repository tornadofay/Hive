using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed class HiveComboBoxExample : IHiveExample
{
    public string Category => "UI";

    public string Subcategory => "Foundation";

    public int Order => 20;

    public string Title => "HiveComboBox";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return new HiveComboBoxExampleView(services);
    }
}
