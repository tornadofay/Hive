# Phase 1.8 — Transactional Outbox Poller

The Phase 1.8 public persistence boundary is exposed through `Hive.Persistence`.

Example Host path: Persistence / Events / Outbox Poller / Transactional Outbox Poller

```csharp
var poller = new EventOutboxPoller(store);
var result = await poller.ProcessNextAsync(handler, cancellationToken);
```

Implement `IEventOutboxHandler` at the delivery boundary and use `entry.Envelope.EventId` as the idempotency key.

The poller claims one committed outbox row with a lease, invokes the handler outside the SQL transaction, and deletes the row only after the handler reports success.

A failed or canceled delivery leaves the row leased until the lease expires. Another poller can then reclaim the row. A process crash after claim therefore does not permanently lose the outbox item.

Duplicate delivery remains possible around the external delivery/acknowledgement boundary. Handlers must be idempotent by `EventId`.

Phase 1.8 does not introduce a background host loop or distributed broker. It provides the durable poll/claim/deliver/acknowledge boundary needed by later execution slices.
