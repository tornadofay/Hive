using System.Text.Json;

namespace Hive.Core;

public readonly record struct EventType
{
    public EventType(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Event type is required.", nameof(value));

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;

    public static implicit operator EventType(string value) => new(value);
}

public readonly record struct EventPayloadVersion
{
    public EventPayloadVersion(int value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Payload schema version must be greater than zero.");

        Value = value;
    }

    public int Value { get; }

    public override string ToString() => Value.ToString();

    public static implicit operator EventPayloadVersion(int value) => new(value);
}

public sealed record EventEnvelope
{
    private EventEnvelope(
        EventId eventId,
        DateTimeOffset occurredAtUtc,
        EventType eventType,
        EventPayloadVersion payloadSchemaVersion,
        CorrelationId correlationId,
        CausationId? causationId,
        JsonElement payload)
    {
        EventId = eventId;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
        EventType = eventType;
        PayloadSchemaVersion = payloadSchemaVersion;
        CorrelationId = correlationId;
        CausationId = causationId;
        Payload = payload.Clone();
    }

    public EventId EventId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public EventType EventType { get; }

    public EventPayloadVersion PayloadSchemaVersion { get; }

    public CorrelationId CorrelationId { get; }

    public CausationId? CausationId { get; }

    public JsonElement Payload { get; }

    public static EventEnvelope Create(
        EventId eventId,
        DateTimeOffset occurredAtUtc,
        EventType eventType,
        EventPayloadVersion payloadSchemaVersion,
        CorrelationId correlationId,
        CausationId? causationId,
        JsonElement payload) =>
        new(
            eventId,
            occurredAtUtc,
            eventType,
            payloadSchemaVersion,
            correlationId,
            causationId,
            payload);
}
