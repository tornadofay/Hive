using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hive.Core;

public interface IEventUpcaster
{
    EventType EventType { get; }

    EventPayloadVersion FromVersion { get; }

    EventPayloadVersion ToVersion { get; }

    JsonElement Upcast(JsonElement payload);
}

public interface IEventUpcasterRegistry
{
    void Register(IEventUpcaster upcaster);

    JsonElement UpcastTo(
        EventType eventType,
        EventPayloadVersion currentVersion,
        EventPayloadVersion targetVersion,
        JsonElement payload);
}

public sealed class EventUpcasterRegistry : IEventUpcasterRegistry
{
    private readonly Dictionary<(EventType EventType, int FromVersion), IEventUpcaster> _upcasters = new();

    public void Register(IEventUpcaster upcaster)
    {
        ArgumentNullException.ThrowIfNull(upcaster);

        if (upcaster.ToVersion.Value != upcaster.FromVersion.Value + 1)
        {
            throw new ArgumentException(
                "Event upcasters must advance exactly one payload schema version.",
                nameof(upcaster));
        }

        var key = (upcaster.EventType, upcaster.FromVersion.Value);

        if (!_upcasters.TryAdd(key, upcaster))
        {
            throw new InvalidOperationException(
                $"An upcaster is already registered for event type '{upcaster.EventType}' from version {upcaster.FromVersion.Value}.");
        }
    }

    public JsonElement UpcastTo(
        EventType eventType,
        EventPayloadVersion currentVersion,
        EventPayloadVersion targetVersion,
        JsonElement payload)
    {
        if (currentVersion.Value == targetVersion.Value)
            return payload.Clone();

        if (currentVersion.Value > targetVersion.Value)
        {
            throw new EventSerializationException(
                new Error(
                    "event.schema.future",
                    ErrorCategory.Serialization,
                    $"Event '{eventType}' has payload schema version {currentVersion.Value}, which is newer than the supported version {targetVersion.Value}."));
        }

        var currentPayload = payload.Clone();
        var current = currentVersion.Value;

        while (current < targetVersion.Value)
        {
            if (!_upcasters.TryGetValue((eventType, current), out var upcaster))
            {
                throw new EventSerializationException(
                    new Error(
                        "event.schema.upcaster-missing",
                        ErrorCategory.Serialization,
                        $"No upcaster is registered for event '{eventType}' from payload schema version {current} to {current + 1}."));
            }

            currentPayload = upcaster.Upcast(currentPayload).Clone();
            current++;
        }

        return currentPayload;
    }
}

public sealed class JsonEventSerializer
{
    public static JsonSerializerOptions CreateDefaultOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new EventIdJsonConverter());
        options.Converters.Add(new CorrelationIdJsonConverter());
        options.Converters.Add(new CausationIdJsonConverter());
        options.Converters.Add(new EventTypeJsonConverter());
        options.Converters.Add(new EventPayloadVersionJsonConverter());

        return options;
    }

    public JsonSerializerOptions Options { get; }

    public JsonEventSerializer(
        IEventUpcasterRegistry? upcasterRegistry = null,
        JsonSerializerOptions? options = null)
    {
        UpcasterRegistry = upcasterRegistry ?? new EventUpcasterRegistry();
        Options = options ?? CreateDefaultOptions();
    }

    public IEventUpcasterRegistry UpcasterRegistry { get; }

    public EventEnvelope CreateEnvelope<TPayload>(
        EventId eventId,
        DateTimeOffset occurredAtUtc,
        EventType eventType,
        EventPayloadVersion payloadSchemaVersion,
        CorrelationId correlationId,
        CausationId? causationId,
        TPayload payload)
    {
        var payloadElement = JsonSerializer.SerializeToElement(payload, Options);

        return EventEnvelope.Create(
            eventId,
            occurredAtUtc,
            eventType,
            payloadSchemaVersion,
            correlationId,
            causationId,
            payloadElement);
    }

    public string SerializeEnvelope(EventEnvelope envelope) =>
        JsonSerializer.Serialize(envelope, Options);

    public EventEnvelope DeserializeEnvelope(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new EventSerializationException(
                new Error(
                    "event.json.empty",
                    ErrorCategory.Serialization,
                    "Event envelope JSON is required."));

        try
        {
            var envelope = JsonSerializer.Deserialize<EventEnvelope>(json, Options);

            return envelope ?? throw new EventSerializationException(
                new Error(
                    "event.json.null",
                    ErrorCategory.Serialization,
                    "The event envelope JSON produced no envelope."));
        }
        catch (EventSerializationException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new EventSerializationException(
                new Error(
                    "event.json.invalid",
                    ErrorCategory.Serialization,
                    $"Event envelope JSON is invalid: {exception.Message}"),
                exception);
        }
    }

    public TPayload DeserializePayload<TPayload>(
        EventEnvelope envelope,
        EventPayloadVersion supportedVersion)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (envelope.PayloadSchemaVersion.Value > supportedVersion.Value)
        {
            throw new EventSerializationException(
                new Error(
                    "event.schema.future",
                    ErrorCategory.Serialization,
                    $"Event '{envelope.EventType}' has unsupported future payload schema version {envelope.PayloadSchemaVersion.Value}."));
        }

        try
        {
            var payload = UpcasterRegistry.UpcastTo(
                envelope.EventType,
                envelope.PayloadSchemaVersion,
                supportedVersion,
                envelope.Payload);

            return JsonSerializer.Deserialize<TPayload>(payload, Options)
                ?? throw new EventSerializationException(
                    new Error(
                        "event.payload.null",
                        ErrorCategory.Serialization,
                        $"Event '{envelope.EventType}' payload deserialized to null."));
        }
        catch (EventSerializationException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new EventSerializationException(
                new Error(
                    "event.payload.invalid",
                    ErrorCategory.Serialization,
                    $"Event '{envelope.EventType}' payload is invalid: {exception.Message}"),
                exception);
        }
    }
}

public sealed class EventSerializationException : Exception
{
    public EventSerializationException(Error error, Exception? innerException = null)
        : base(error.Message, innerException)
    {
        Error = error;
    }

    public Error Error { get; }
}
