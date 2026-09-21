using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new ThemeFoundationExampleForm());
    }
}
