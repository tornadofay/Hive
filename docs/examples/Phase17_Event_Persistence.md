# Phase 1.7 — Event Log, Snapshot & Transactional Outbox

The Phase 1.7 event log, snapshot, and transactional outbox primitive is an internal Hive persistence boundary. Application-facing consumers must not construct or call the raw event persistence store directly because that would bypass the `Hive.Management` authorization boundary.

Trusted Hive composition uses the opaque event-persistence composition handle:

```csharp
var persistence = HiveEventPersistence.CreateSql(options);
```

The composition handle does not expose `IEventPersistenceStore` or `SqlEventPersistenceStore`. Hive's Management and Coordination layers use that internal persistence port to perform authorized application operations and execution lifecycle persistence.

A successful internal append writes the event, optional current snapshot, and corresponding outbox row in one SQL transaction.

The next append must provide the stream's current `ResourceVersion` as its expected version. A stale expected version is returned as a Concurrency error rather than silently creating a conflicting stream version.

Events can be read in stream order and folded through a registered `IEventStateReducer<TState>`. Older supported payloads are upcast through the existing `JsonEventSerializer` and `IEventUpcasterRegistry` before the reducer sees them.

Phase 1.7 does not expose arbitrary event-stream reads or writes as a public application API. Public examples exercise the durable event behavior through owning Management/Coordination operations. Outbox delivery is demonstrated separately by the Phase 1.8 public poller boundary.
