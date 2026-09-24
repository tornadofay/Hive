using System.Text.Json;

namespace Hive.Core;

public readonly record struct EventType
{
    private const int MaxValueLength = 200;
    private readonly bool _isValid;

    public EventType(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Event type is required.", nameof(value));

        var normalized = value.Trim();

        if (normalized.Length > MaxValueLength)
            throw new ArgumentException(
                $"Event type cannot exceed {MaxValueLength} characters.",
                nameof(value));

        Value = normalized;
        _isValid = true;
    }

    public string Value { get; }

    internal bool IsValid => _isValid;

    public override string ToString() => Value;

    public static implicit operator EventType(string value) => new(value);
}

public readonly record struct EventPayloadVersion
{
    private readonly bool _isValid;

    public EventPayloadVersion(int value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Payload schema version must be greater than zero.");

        Value = value;
        _isValid = true;
    }

    public int Value { get; }

    internal bool IsValid => _isValid;

    public override string ToString() => Value.ToString();

    public static implicit operator EventPayloadVersion(int value) => new(value);
}

public sealed record EventEnvelope
{
    public EventEnvelope(
        EventId eventId,
        DateTimeOffset occurredAtUtc,
        EventType eventType,
        EventPayloadVersion payloadSchemaVersion,
        CorrelationId correlationId,
        CausationId? causationId,
        JsonElement payload)
    {
        if (eventId == default)
            throw new ArgumentException(
                "EventId is required.",
                nameof(eventId));

        if (!eventType.IsValid)
            throw new ArgumentException(
                "A valid event type is required.",
                nameof(eventType));

        if (!payloadSchemaVersion.IsValid)
            throw new ArgumentException(
                "A valid event payload schema version is required.",
                nameof(payloadSchemaVersion));

        if (correlationId == default)
            throw new ArgumentException(
                "CorrelationId is required.",
                nameof(correlationId));

        if (causationId is { } causation && causation == default)
            throw new ArgumentException(
                "CausationId must be non-empty when supplied.",
                nameof(causationId));

        if (payload.ValueKind == JsonValueKind.Undefined)
            throw new ArgumentException(
                "Event payload must be a defined JSON value.",
                nameof(payload));

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
