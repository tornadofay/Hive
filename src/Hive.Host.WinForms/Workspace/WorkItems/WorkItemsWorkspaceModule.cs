namespace Hive.Host.WinForms;

internal sealed class WorkItemsWorkspaceModule : IHiveWorkspaceModule
{
    public string Id => "work-items";

    public string Title => "Work Items";

    public int Order => 20;

    public Control CreateView(HiveWorkspaceContext context) =>
        new WorkItemsView(context);
}
