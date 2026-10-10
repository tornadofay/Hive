using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal sealed class HiveDirectLlmConversationExample : IHiveExample
{
    public string Category => "Workspace";

    public string Subcategory => "Direct LLM";

    public int Order => 20;

    public string Title => "Direct LLM Conversation";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new HiveDirectLlmConversationExampleView(services);
    }
}
