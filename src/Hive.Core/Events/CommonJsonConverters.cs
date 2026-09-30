using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hive.Core;

internal sealed class EventIdJsonConverter : JsonConverter<EventId>
{
    public override EventId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (value is null || !EventId.TryParse(value, out var id))
            throw new JsonException("Invalid EventId value.");

        return id;
    }

    public override void Write(Utf8JsonWriter writer, EventId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}

internal sealed class CorrelationIdJsonConverter : JsonConverter<CorrelationId>
{
    public override CorrelationId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (value is null || !CorrelationId.TryParse(value, out var id))
            throw new JsonException("Invalid CorrelationId value.");

        return id;
    }

    public override void Write(Utf8JsonWriter writer, CorrelationId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}

internal sealed class CausationIdJsonConverter : JsonConverter<CausationId>
{
    public override CausationId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (value is null || !CausationId.TryParse(value, out var id))
            throw new JsonException("Invalid CausationId value.");

        return id;
    }

    public override void Write(Utf8JsonWriter writer, CausationId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}

internal sealed class EventTypeJsonConverter : JsonConverter<EventType>
{
    public override EventType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (value is null)
            throw new JsonException("Event type value is required.");

        try
        {
            return new EventType(value);
        }
        catch (ArgumentException exception)
        {
            throw new JsonException("Invalid event type value.", exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, EventType value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}

internal sealed class EventPayloadVersionJsonConverter : JsonConverter<EventPayloadVersion>
{
    public override EventPayloadVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (!reader.TryGetInt32(out var value))
            throw new JsonException("Event payload schema version must be an integer.");

        try
        {
            return new EventPayloadVersion(value);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new JsonException("Invalid event payload schema version.", exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, EventPayloadVersion value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Value);
}
