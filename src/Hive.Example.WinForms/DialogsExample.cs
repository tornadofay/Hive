using System.Windows.Forms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed class DialogsExample : IHiveExample
{
    public string Category => "UI";

    public string Subcategory => "Foundation";

    public string Title => "Dialogs";

    public UserControl CreateView(IServiceProvider services)
    {
        var themeManager = services.GetService(typeof(IHiveThemeManager))
            as IHiveThemeManager
            ?? throw new InvalidOperationException(
                "The Example host did not provide IHiveThemeManager.");

        return new DialogsExampleView(themeManager);
    }
}
