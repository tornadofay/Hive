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

        using var deliveryCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var renewalTask = RenewLeaseUntilCompletedAsync(
            workItem,
            deliveryCts);

        Hive.Core.Result? delivery = null;
        OperationCanceledException? handlerCancellation = null;

        try
        {
            try
            {
                delivery = await handler.HandleAsync(
                    workItem.Entry,
                    deliveryCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception)
            {
                handlerCancellation = exception;
            }
            catch (Exception exception)
            {
                delivery = Hive.Core.Result.Failure(
                    HivePersistenceError.External(
                        "hive.outbox.handler",
                        "Outbox delivery failed.",
                        exception));
            }
        }
        catch (Exception exception)
        {
            delivery = Hive.Core.Result.Failure(
                HivePersistenceError.Internal(
                    "hive.outbox.handler",
                    "Outbox delivery failed.",
                    exception));
        }

        if (handlerCancellation is not null)
        {
            deliveryCts.Cancel();
            var renewalAfterCancellation = await renewalTask.ConfigureAwait(false);

            if (renewalAfterCancellation.IsFailure)
                return Hive.Core.Result<EventOutboxEntry?>.Failure(renewalAfterCancellation.Error!);

            throw handlerCancellation;
        }

        if (delivery is null)
        {
            deliveryCts.Cancel();
            var renewalAfterMissingResult = await renewalTask.ConfigureAwait(false);

            if (renewalAfterMissingResult.IsFailure)
                return Hive.Core.Result<EventOutboxEntry?>.Failure(renewalAfterMissingResult.Error!);

            return Hive.Core.Result<EventOutboxEntry?>.Failure(
                HivePersistenceError.Internal(
                    "hive.outbox.delivery-result-missing",
                    "Outbox delivery completed without a result.",
                    new InvalidOperationException(
                        "The outbox handler returned without producing a Result.")));
        }

        var deliveryResult = delivery.Value;
        if (deliveryResult.IsFailure)
        {
            deliveryCts.Cancel();
            var renewalAfterFailure = await renewalTask.ConfigureAwait(false);

            if (renewalAfterFailure.IsFailure)
                return Hive.Core.Result<EventOutboxEntry?>.Failure(renewalAfterFailure.Error!);

            return Hive.Core.Result<EventOutboxEntry?>.Failure(deliveryResult.Error!);
        }

        Hive.Core.Result completed;
        try
        {
            // Keep the lease renewal alive through acknowledgement. A successful
            // handler must not lose its lease while CompleteOutboxAsync is still
            // performing the durable acknowledgement.
            completed = await _store.CompleteOutboxAsync(
                workItem,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            deliveryCts.Cancel();
            await renewalTask.ConfigureAwait(false);
        }

        if (completed.IsFailure)
            return Hive.Core.Result<EventOutboxEntry?>.Failure(completed.Error!);

        return Hive.Core.Result<EventOutboxEntry?>.Success(workItem.Entry);
    }

    private async Task<Hive.Core.Result> RenewLeaseUntilCompletedAsync(
        EventOutboxWorkItem workItem,
        CancellationTokenSource deliveryCts)
    {
        var cancellationToken = deliveryCts.Token;
        var interval = TimeSpan.FromTicks(
            Math.Max(
                TimeSpan.FromMilliseconds(1).Ticks,
                _leaseDuration.Ticks / 2));

        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken)
                .ConfigureAwait(false))
            {
                var renewed = await _store.RenewOutboxLeaseAsync(
                    workItem,
                    _leaseDuration,
                    cancellationToken).ConfigureAwait(false);

                if (renewed.IsFailure)
                {
                    deliveryCts.Cancel();
                    return Hive.Core.Result.Failure(renewed.Error!);
                }
            }

            return Hive.Core.Result.Success();
        }
        catch (OperationCanceledException)
        {
            return Hive.Core.Result.Success();
        }
        catch (Exception exception)
        {
            deliveryCts.Cancel();
            return Hive.Core.Result.Failure(
                HivePersistenceError.External(
                    "hive.outbox.lease-renewal",
                    "Outbox lease renewal failed.",
                    exception));
        }
    }
}
