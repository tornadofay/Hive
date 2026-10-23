using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms;

/// <summary>
/// The independent, modeless Workspace window. Open it through <see cref="HiveWorkspaceLauncher"/>
/// so a host keeps a single instance.
/// </summary>
public sealed class HiveWorkspaceForm : HiveForm
{
    private readonly HiveWorkspaceView _workspace;

    public HiveWorkspaceForm(HiveWorkspaceContext context)
        : this(context, null)
    {
    }

    internal HiveWorkspaceForm(
        HiveWorkspaceContext context,
        IEnumerable<IHiveWorkspaceModule>? modules)
        : base(
            "Hive Workspace",
            "Chat and work over Hive.Management",
            new Size(1180, 780),
            new Size(1040, 680),
            ThemeOf(context))
    {
        ConfigureHeader(
            allowMove: true,
            allowClose: true,
            allowMinimize: true,
            allowMaximize: true,
            allowHelp: false,
            allowThemeToggle: true);

        SetBodyPadding(new Padding(12));

        _workspace = modules is null
            ? new HiveWorkspaceView(context)
            : new HiveWorkspaceView(context, modules);
        _workspace.Dock = DockStyle.Fill;

        BodyPanel.Controls.Add(_workspace);
    }

    public HiveWorkspaceView Workspace => _workspace;

    private static IHiveThemeManager ThemeOf(HiveWorkspaceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.ThemeManager;
    }
}
