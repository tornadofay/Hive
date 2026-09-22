namespace Hive.Example.WinForms;

internal sealed class EventOutboxPollerExample : IHiveExample
{
    public string Category => "Persistence";
    public string Subcategory => "Events";
    public IReadOnlyList<string> AdditionalNavigationPath => ["Outbox Poller"];
    public int Order => 10;
    public string Title => "Transactional Outbox Poller";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new EventOutboxPollerExampleView(services.GetExampleOutput());
    }
}
