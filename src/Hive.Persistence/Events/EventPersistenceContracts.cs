using System.Text.Json;
using Hive.Core;

namespace Hive.Persistence;

public sealed class EventSnapshot
{
    public EventSnapshot(
        ResourceReference stream,
        ResourceVersion version,
        EventPayloadVersion payloadSchemaVersion,
        JsonElement state)
    {
        if (!Enum.IsDefined(stream.Kind))
            throw new ArgumentOutOfRangeException(nameof(stream), "Event stream resource kind is invalid.");

        if (stream.Identity == Guid.Empty)
            throw new ArgumentException(
                "Event stream identity is required.",
                nameof(stream));

        if (version.Value <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(version),
                version.Value,
                "Snapshot version must be greater than zero.");

        if (payloadSchemaVersion.Value <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(payloadSchemaVersion),
                payloadSchemaVersion.Value,
                "Snapshot payload schema version must be greater than zero.");

        Version = version;
        PayloadSchemaVersion = payloadSchemaVersion;

        Stream = stream;
        State = state.ValueKind == JsonValueKind.Undefined
            ? throw new ArgumentException("Snapshot state is required.", nameof(state))
            : state.Clone();
    }

    public ResourceReference Stream { get; }

    public ResourceVersion Version { get; }

    public EventPayloadVersion PayloadSchemaVersion { get; }

    public JsonElement State { get; }
}

public sealed class PersistedEvent
{
    public PersistedEvent(
        ResourceReference stream,
        ResourceVersion streamVersion,
        EventEnvelope envelope)
    {
        if (!Enum.IsDefined(stream.Kind))
            throw new ArgumentOutOfRangeException(nameof(stream), "Event stream resource kind is invalid.");

        if (stream.Identity == Guid.Empty)
            throw new ArgumentException(
                "Event stream identity is required.",
                nameof(stream));

        if (streamVersion.Value <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(streamVersion),
                streamVersion.Value,
                "Event stream version must be greater than zero.");

        Stream = stream;
        StreamVersion = streamVersion;
        Envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));
    }

    public ResourceReference Stream { get; }

    public ResourceVersion StreamVersion { get; }

    public EventEnvelope Envelope { get; }
}

public sealed class EventOutboxEntry
{
    public EventOutboxEntry(
        ResourceReference stream,
        ResourceVersion streamVersion,
        EventEnvelope envelope)
    {
        if (!Enum.IsDefined(stream.Kind))
            throw new ArgumentOutOfRangeException(nameof(stream), "Event stream resource kind is invalid.");

        Stream = stream;
        StreamVersion = streamVersion;
        Envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));
    }

    public ResourceReference Stream { get; }

    public ResourceVersion StreamVersion { get; }

    public EventEnvelope Envelope { get; }
}

public sealed class EventOutboxWorkItem
{
    public EventOutboxWorkItem(
        EventOutboxEntry entry,
        Guid leaseId,
        int attemptCount)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));

        if (leaseId == Guid.Empty)
            throw new ArgumentException("Outbox lease identity is required.", nameof(leaseId));

        if (attemptCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(attemptCount));

        LeaseId = leaseId;
        AttemptCount = attemptCount;
    }

    public EventOutboxEntry Entry { get; }

    public Guid LeaseId { get; }

    public int AttemptCount { get; }
}

public interface IEventOutboxPollerStore
{
    Task<Result<EventOutboxWorkItem?>> ClaimNextOutboxAsync(
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task<Result> CompleteOutboxAsync(
        EventOutboxWorkItem workItem,
        CancellationToken cancellationToken = default);
}

public interface IEventOutboxHandler
{
    Task<Result> HandleAsync(
        EventOutboxEntry entry,
        CancellationToken cancellationToken = default);
}

public sealed class EventAppendRequest
{
    public EventAppendRequest(
        ResourceReference stream,
        ResourceVersion? expectedVersion,
        EventEnvelope envelope,
        EventSnapshot? snapshot = null)
    {
        if (!Enum.IsDefined(stream.Kind))
            throw new ArgumentOutOfRangeException(nameof(stream), "Event stream resource kind is invalid.");

        if (stream.Identity == Guid.Empty)
            throw new ArgumentException("Event stream identity is required.", nameof(stream));

        Envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));
        Stream = stream;

        if (expectedVersion is { } suppliedExpectedVersion &&
            suppliedExpectedVersion.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedVersion),
                suppliedExpectedVersion.Value,
                "Expected event stream version must be greater than zero when supplied.");
        }

        ExpectedVersion = expectedVersion;

        var expectedValue = expectedVersion?.Value ?? 0;

        if (expectedValue == long.MaxValue)
            throw new InvalidOperationException("Event stream version limit reached.");

        StreamVersion = new ResourceVersion(expectedValue + 1);
        Snapshot = snapshot;

        if (snapshot is not null)
        {
            if (snapshot.Stream != stream)
            {
                throw new ArgumentException(
                    "Snapshot stream must match the event stream.",
                    nameof(snapshot));
            }

            if (snapshot.Version != StreamVersion)
            {
                throw new ArgumentException(
                    "Snapshot version must match the event's next stream version.",
                    nameof(snapshot));
            }
        }

        if (envelope.EventId == default)
        {
            throw new ArgumentException("Event identity is required.", nameof(envelope));
        }
    }

    public ResourceReference Stream { get; }

    public ResourceVersion? ExpectedVersion { get; }

    public ResourceVersion StreamVersion { get; }

    public EventEnvelope Envelope { get; }

    public EventSnapshot? Snapshot { get; }
}

public sealed class EventAppendResult
{
    public EventAppendResult(
        PersistedEvent @event,
        EventSnapshot? snapshot,
        EventOutboxEntry outbox)
    {
        Event = @event ?? throw new ArgumentNullException(nameof(@event));
        Snapshot = snapshot;
        Outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
    }

    public PersistedEvent Event { get; }

    public EventSnapshot? Snapshot { get; }

    public EventOutboxEntry Outbox { get; }
}

public interface IEventPersistenceStore
{
    Task<Result<EventAppendResult>> AppendAsync(
        EventAppendRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PersistedEvent>>> ReadEventsAsync(
        ResourceReference stream,
        ResourceVersion? afterVersion = null,
        CancellationToken cancellationToken = default);

    Task<Result<EventSnapshot?>> GetSnapshotAsync(
        ResourceReference stream,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<EventSnapshot>>> ListSnapshotsAsync(
        ResourceKind streamKind,
        CancellationToken cancellationToken = default);

    Task<Result<EventOutboxEntry?>> GetOutboxAsync(
        EventId eventId,
        CancellationToken cancellationToken = default);
}
