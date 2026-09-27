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
        Application.Run(new HiveExampleHostForm());
    }

    private static void OnThreadException(
        object? sender,
        ThreadExceptionEventArgs e)
    {
        var owner = Application.OpenForms.Count > 0
            ? Application.OpenForms[0]
            : null;

        HiveUiErrorReporter.Report(
            owner,
            e.Exception,
            "Unhandled UI exception",
            "An unexpected UI error occurred. The technical details are shown below.");
    }
}
