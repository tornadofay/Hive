namespace Hive.Host.WinForms;

/// <summary>
/// A feature hosted by the Workspace shell. The shell owns navigation and the content host only;
/// each module owns its own view. Later phases add modules without editing the shell.
/// </summary>
internal interface IHiveWorkspaceModule
{
    string Id { get; }

    string Title { get; }

    int Order { get; }

    Control CreateView(HiveWorkspaceContext context);
}

/// <summary>
/// Implemented by a module view that loads data. The shell calls it when the module is first shown
/// and when the Management graph is replaced.
/// </summary>
internal interface IHiveWorkspaceRefreshable
{
    Task RefreshAsync(CancellationToken cancellationToken);
}
