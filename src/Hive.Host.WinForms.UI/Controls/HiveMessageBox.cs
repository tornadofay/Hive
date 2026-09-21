using System.Windows.Forms;
using ReaLTaiizor.Forms;

namespace Hive.Host.WinForms.UI.Controls;

public static class HiveMessageBox
{
    public static DialogResult Show(
        IWin32Window? owner,
        string message,
        string title,
        MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.Information)
    {
        return owner is null
            ? MaterialMessageBox.Show(message, title, buttons, icon)
            : MaterialMessageBox.Show(owner, message, title, buttons, icon);
    }

    public static DialogResult Show(
        string message,
        string title,
        MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.Information) =>
        Show(null, message, title, buttons, icon);
}
