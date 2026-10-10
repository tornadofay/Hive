using Hive.Host.WinForms;
using Hive.Host.WinForms.UI.Controls;

namespace Hive.Example.WinForms;

internal sealed class HiveDirectLlmConversationExampleView : UserControl
{
    private readonly HiveDirectLlmConversationView _conversationView;

    public HiveDirectLlmConversationExampleView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var hiveServices = services.GetHiveExampleServices();

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        var introduction = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 2, 8, 8),
            Text = "Direct LLM mode sends chat messages to the exact active ExecutionTarget you select. Conversations and request outcomes are saved through Hive.Management and can be reloaded."
        };

        _conversationView = new HiveDirectLlmConversationView(
            hiveServices.GetManagementFacade(),
            hiveServices.AccessContext,
            hiveServices.GetThemeManager())
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(introduction, 0, 0);
        layout.Controls.Add(_conversationView, 0, 1);

        Controls.Add(layout);
        hiveServices.GetThemeManager().Apply(this);
    }
}
