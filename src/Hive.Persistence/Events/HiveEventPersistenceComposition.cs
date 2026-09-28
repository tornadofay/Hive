using Hive.Core;

namespace Hive.Persistence;

public sealed class HiveEventPersistenceComposition
{
    internal HiveEventPersistenceComposition(
        IEventPersistenceStore eventStore)
    {
        EventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
    }

    internal IEventPersistenceStore EventStore { get; }

    public EventOutboxPoller CreateOutboxPoller(
        TimeSpan? leaseDuration = null) =>
        leaseDuration is { } duration
            ? new EventOutboxPoller(EventStore, duration)
            : new EventOutboxPoller(EventStore);
}

public static class HiveEventPersistence
{
    public static HiveEventPersistenceComposition CreateSql(
        HiveDatabaseOptions options,
        IClock? clock = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new HiveEventPersistenceComposition(
            new SqlEventPersistenceStore(
                options,
                clock: clock));
    }
}
