namespace Hive.Persistence;

public sealed class EventOutboxPoller
{
    private static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromSeconds(30);

    private readonly IEventOutboxPollerStore _store;
    private readonly TimeSpan _leaseDuration;

    public EventOutboxPoller(IEventOutboxPollerStore store)
        : this(store, DefaultLeaseDuration)
    {
    }

    public EventOutboxPoller(IEventOutboxPollerStore store, TimeSpan leaseDuration)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        if (leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        _leaseDuration = leaseDuration;
    }

    public async Task<Hive.Core.Result<EventOutboxEntry?>> ProcessNextAsync(
        IEventOutboxHandler handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var claimed = await _store.ClaimNextOutboxAsync(
            _leaseDuration,
            cancellationToken).ConfigureAwait(false);

        if (claimed.IsFailure)
            return Hive.Core.Result<EventOutboxEntry?>.Failure(claimed.Error!);

        var workItem = claimed.Value;
        if (workItem is null)
            return Hive.Core.Result<EventOutboxEntry?>.Success(null);

        Hive.Core.Result delivery;
        try
        {
            delivery = await handler.HandleAsync(
                workItem.Entry,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Hive.Core.Result<EventOutboxEntry?>.Failure(
                HivePersistenceError.External(
                    "hive.outbox.handler",
                    "Outbox delivery failed.",
                    exception));
        }

        if (delivery.IsFailure)
            return Hive.Core.Result<EventOutboxEntry?>.Failure(delivery.Error!);

        var completed = await _store.CompleteOutboxAsync(
            workItem,
            cancellationToken).ConfigureAwait(false);

        if (completed.IsFailure)
            return Hive.Core.Result<EventOutboxEntry?>.Failure(completed.Error!);

        return Hive.Core.Result<EventOutboxEntry?>.Success(workItem.Entry);
    }
}
