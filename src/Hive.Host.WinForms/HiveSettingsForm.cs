using System.Drawing;
using System.Windows.Forms;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Host.WinForms;

public sealed class HiveSettingsForm : HiveForm
{
    public HiveSettingsForm(
        IHiveManagementFacade management,
        ResourceAccessContext accessContext,
        IHiveThemeManager themeManager,
        IHiveExampleOutput? output = null)
        : base(
            "Hive Settings",
            "Global Hive package configuration",
            new Size(1120, 780),
            new Size(880, 620),
            themeManager)
    {
        ArgumentNullException.ThrowIfNull(management);
        ArgumentNullException.ThrowIfNull(accessContext);
        ArgumentNullException.ThrowIfNull(themeManager);

        ConfigureHeader(
            allowMove: true,
            allowClose: true,
            allowMinimize: false,
            allowMaximize: true,
            allowHelp: false,
            allowThemeToggle: true);

        SetBodyPadding(new Padding(12));

        var view = new HiveSettingsView(
            management,
            accessContext,
            themeManager,
            Application.ProductName,
            output);

        view.Dock = DockStyle.Fill;
        BodyPanel.Controls.Add(view);
    }
}
