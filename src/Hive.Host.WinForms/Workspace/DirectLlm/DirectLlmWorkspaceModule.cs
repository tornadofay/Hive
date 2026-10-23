namespace Hive.Host.WinForms;

internal sealed class DirectLlmWorkspaceModule : IHiveWorkspaceModule
{
    public string Id => "direct-llm";

    public string Title => "Direct LLM";

    public int Order => 10;

    public Control CreateView(HiveWorkspaceContext context) =>
        new DirectLlmConversationView(context);
}
