using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

public static class HiveUiErrorReporter
{
    public static void Report(
        IWin32Window? owner,
        Exception exception,
        string title,
        string message,
        IHiveExampleOutput? output = null,
        IHiveThemeManager? themeManager = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var safeMessage = string.IsNullOrWhiteSpace(message)
            ? exception.Message
            : message;

        output?.Write("EXCEPTION", exception.ToString());

        HiveMessageBox.Show(
            owner,
            new HiveMessageOptions(
                title,
                safeMessage,
                HiveMessageType.Error,
                MessageBoxButtons.OK,
                exception.ToString(),
                DetailsExpanded: true),
            themeManager);
    }

    public static void Report(
        IWin32Window? owner,
        string message,
        string title,
        IHiveExampleOutput? output = null,
        IHiveThemeManager? themeManager = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            message = "The requested operation failed.";

        output?.Write("ERROR", message);

        HiveMessageBox.ShowError(
            owner,
            message,
            title,
            themeManager);
    }
}
