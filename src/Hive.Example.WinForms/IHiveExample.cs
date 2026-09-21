using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal interface IHiveExample
{
    string Category { get; }

    string Subcategory { get; }

    string Title { get; }

    UserControl CreateView(IServiceProvider services);
}
