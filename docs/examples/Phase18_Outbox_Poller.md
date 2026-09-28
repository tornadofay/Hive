# Phase 1.8 — Transactional Outbox Poller

The transactional outbox delivery boundary is public through the poller and its handler contract. The SQL event persistence implementation itself remains internal.

Example Host path: Persistence / Events / Outbox Poller / Transactional Outbox Poller

```csharp
var persistence = HiveEventPersistence.CreateSql(options);
var poller = persistence.CreateOutboxPoller();

var result = await poller.ProcessNextAsync(
    handler,
    cancellationToken);
```

Use an application-facing Management operation or another owning Hive boundary to cause the durable state change that creates the outbox row. Do not call the raw event store from application code.

Implement `IEventOutboxHandler` at the delivery boundary and use `entry.Envelope.EventId` as the idempotency key.

The poller claims one committed outbox row with a lease, invokes the handler outside the SQL transaction, renews the lease while the handler is still running, and deletes the row only after the handler reports success. If lease renewal is lost, the delivery token is canceled and the row remains recoverable.

A failed or canceled delivery leaves the row leased until the lease expires. Another poller can then reclaim the row. A process crash after claim therefore does not permanently lose the outbox item.

Duplicate delivery remains possible around the external delivery/acknowledgement boundary. Handlers must be idempotent by `EventId`.

Phase 1.8 does not introduce a background host loop or distributed broker. It provides the durable poll/claim/deliver/acknowledge boundary needed by later execution slices.
