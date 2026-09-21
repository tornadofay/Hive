using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal sealed class PlaceholderForm : Form
{
    public PlaceholderForm()
    {
        Text = "Hive Example";
        Width = 900;
        Height = 600;
        StartPosition = FormStartPosition.CenterScreen;
    }
}
