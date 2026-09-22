using System.Windows.Forms;
using Hive.Host.WinForms.UI.Controls;

namespace Hive.Example.WinForms;

internal sealed class DialogsExample : IHiveExample
{
    public string Category => "UI";

    public string Subcategory => "Foundation";

    public int Order => 30;

    public string Title => "Dialogs";

    public UserControl CreateView(IServiceProvider services)
    {
        var themeManager = services.GetThemeManager();

        return new DialogsExampleView(themeManager);
    }
}
