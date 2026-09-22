# Phase 1.7 — Event Log, Snapshot & Transactional Outbox

The Phase 1.7 public persistence boundary is exposed through Hive.Persistence.

Example Host path: Persistence / Events / Event Persistence / Event Log + Snapshot + Outbox

```csharp
var options = HiveDatabaseOptions.LocalDevelopment(
    "Hive_Example_EventPersistence");

var migration = await new HiveDatabaseMigrator(options)
    .MigrateAsync(cancellationToken);

if (migration.IsFailure)
    throw new InvalidOperationException(migration.Error?.Message);

var store = new SqlEventPersistenceStore(options);
var stream = new ResourceReference(
    ResourceKind.WorkItem,
    Guid.NewGuid());

var serializer = new JsonEventSerializer();

var envelope = serializer.CreateEnvelope(
    EventId.New(),
    DateTimeOffset.UtcNow,
    new EventType("example.work-item.created"),
    new EventPayloadVersion(1),
    CorrelationId.New(),
    null,
    new { status = "Created" });

var snapshot = new EventSnapshot(
    stream,
    ResourceVersion.Initial,
    new EventPayloadVersion(1),
    JsonSerializer.SerializeToElement(new { status = "Created" }));

var append = await store.AppendAsync(
    new EventAppendRequest(
        stream,
        expectedVersion: null,
        envelope,
        snapshot),
    cancellationToken);
```

A successful append writes the event, optional current snapshot, and corresponding outbox row in one SQL transaction.

The next append must provide the stream's current ResourceVersion as expectedVersion. A stale expected version is returned as a Concurrency error rather than silently creating a conflicting stream version.

Events can be read in stream order and folded through a registered IEventStateReducer<TState>. Older supported payloads are upcast through the existing JsonEventSerializer and IEventUpcasterRegistry before the reducer sees them.

Phase 1.7 does not process or dispatch outbox rows. Outbox polling/processing belongs to Phase 1.8.
