namespace Hive.Example.WinForms;

internal sealed class EventPersistenceExample : IHiveExample
{
    public string Category => "Persistence";

    public string Subcategory => "Events";

    public int Order => 10;

    public string Title => "Event Log / Snapshot / Outbox";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new EventPersistenceExampleView(services.GetExampleOutput());
    }
}
