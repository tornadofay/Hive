using System.Windows.Forms;

namespace Hive.Host.WinForms.UI.Theme;

public interface IHiveThemeManager
{
    HiveThemeMode Mode { get; }
    HiveThemeDefinition Theme { get; }

    event EventHandler? ThemeChanged;

    void SetMode(HiveThemeMode mode);

    void Apply(Control root);
}
