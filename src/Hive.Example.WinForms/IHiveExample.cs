using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal interface IHiveExample
{
    string Category { get; }

    string Subcategory { get; }

    IReadOnlyList<string> AdditionalNavigationPath => Array.Empty<string>();

    IReadOnlyList<string> NavigationPath =>
        new[] { Category, Subcategory }.Concat(AdditionalNavigationPath).ToArray();

    string Title { get; }

    int Order => 0;

    UserControl CreateView(IServiceProvider services);
}
