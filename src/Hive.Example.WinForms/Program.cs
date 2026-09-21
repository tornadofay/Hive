using System.Windows.Forms;
using Hive.Host.WinForms.UI.Controls;

namespace Hive.Example.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += OnThreadException;
        Application.Run(new ThemeFoundationExampleForm());
    }

    private static void OnThreadException(
        object? sender,
        ThreadExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine(e.Exception.ToString());

        var owner = Application.OpenForms.Count > 0
            ? Application.OpenForms[0]
            : null;

        HiveMessageBox.Show(
            owner,
            new HiveMessageOptions(
                "Unhandled UI exception",
                "An unexpected UI error occurred. The technical details are shown below.",
                HiveMessageType.Error,
                MessageBoxButtons.OK,
                e.Exception.ToString(),
                DetailsExpanded: true));
    }
}
