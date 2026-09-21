using Hive.Core;
using System.Text.Json;
using Xunit;

namespace Hive.Tests;

public sealed class EventInfrastructureTests
{
    [Fact]
    public void EventEnvelope_NormalizesTimeAndClonesPayload()
    {
        var eventId = EventId.New();
        var correlationId = CorrelationId.New();
        var causationId = CausationId.New();
        using var document = JsonDocument.Parse("""{"name":"Alice"}""");

        var envelope = EventEnvelope.Create(
            eventId,
            new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.FromHours(2)),
            new EventType("customer.created"),
            new EventPayloadVersion(1),
            correlationId,
            causationId,
            document.RootElement);

        Assert.Equal(eventId, envelope.EventId);
        Assert.Equal(new DateTimeOffset(2030, 1, 2, 1, 4, 5, TimeSpan.Zero), envelope.OccurredAtUtc);
        Assert.Equal("customer.created", envelope.EventType.Value);
        Assert.Equal(1, envelope.PayloadSchemaVersion.Value);
        Assert.Equal(correlationId, envelope.CorrelationId);
        Assert.Equal(causationId, envelope.CausationId);
        Assert.Equal("Alice", envelope.Payload.GetProperty("name").GetString());
    }

    [Fact]
    public void EventPayloadVersion_RejectsNonPositiveValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventPayloadVersion(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventPayloadVersion(-1));
    }

    [Fact]
    public void EventType_TrimsAndRejectsBlankValues()
    {
        Assert.Equal("customer.created", new EventType("  customer.created  ").Value);
        Assert.Throws<ArgumentException>(() => new EventType(" "));
    }

    [Fact]
    public void Serializer_RoundTripsEnvelopeWithCommonValues()
    {
        var serializer = new JsonEventSerializer();
        var eventId = EventId.New();
        var correlationId = CorrelationId.New();
        var causationId = CausationId.New();

        var envelope = serializer.CreateEnvelope(
            eventId,
            new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero),
            new EventType("customer.created"),
            new EventPayloadVersion(1),
            correlationId,
            causationId,
            new CustomerCreated("Alice", 42));

        var json = serializer.SerializeEnvelope(envelope);
        var roundTrip = serializer.DeserializeEnvelope(json);

        Assert.Equal(envelope.EventId, roundTrip.EventId);
        Assert.Equal(envelope.OccurredAtUtc, roundTrip.OccurredAtUtc);
        Assert.Equal(envelope.EventType, roundTrip.EventType);
        Assert.Equal(envelope.PayloadSchemaVersion, roundTrip.PayloadSchemaVersion);
        Assert.Equal(envelope.CorrelationId, roundTrip.CorrelationId);
        Assert.Equal(envelope.CausationId, roundTrip.CausationId);
        Assert.Equal("Alice", roundTrip.Payload.GetProperty("name").GetString());
        Assert.Equal(42, roundTrip.Payload.GetProperty("age").GetInt32());

        var payload = serializer.DeserializePayload<CustomerCreated>(
            roundTrip,
            new EventPayloadVersion(1));

        Assert.Equal(new CustomerCreated("Alice", 42), payload);
    }

    [Fact]
    public void Serializer_RejectsEmptyAndMalformedJson()
    {
        var serializer = new JsonEventSerializer();

        var empty = Assert.Throws<EventSerializationException>(
            () => serializer.DeserializeEnvelope("  "));
        Assert.Equal("event.json.empty", empty.Error.Code);

        var malformed = Assert.Throws<EventSerializationException>(
            () => serializer.DeserializeEnvelope("{"));

        Assert.Equal("event.json.invalid", malformed.Error.Code);
        Assert.Equal(ErrorCategory.Serialization, malformed.Error.Category);
    }

    [Fact]
    public void Serializer_RejectsFuturePayloadVersion()
    {
        var serializer = new JsonEventSerializer();

        var envelope = serializer.CreateEnvelope(
            EventId.New(),
            DateTimeOffset.UtcNow,
            new EventType("customer.created"),
            new EventPayloadVersion(3),
            CorrelationId.New(),
            null,
            new CustomerCreated("Alice", 42));

        var exception = Assert.Throws<EventSerializationException>(
            () => serializer.DeserializePayload<CustomerCreated>(
                envelope,
                new EventPayloadVersion(2)));

        Assert.Equal("event.schema.future", exception.Error.Code);
    }

    [Fact]
    public void UpcasterRegistry_UpcastsSequentialVersions()
    {
        var registry = new EventUpcasterRegistry();
        var eventType = new EventType("customer.created");

        registry.Register(new CustomerCreatedV1ToV2Upcaster(eventType));
        registry.Register(new CustomerCreatedV2ToV3Upcaster(eventType));

        using var document = JsonDocument.Parse("""{"name":"Alice"}""");

        var result = registry.UpcastTo(
            eventType,
            new EventPayloadVersion(1),
            new EventPayloadVersion(3),
            document.RootElement);

        Assert.Equal("Alice", result.GetProperty("name").GetString());
        Assert.Equal("customer", result.GetProperty("kind").GetString());
        Assert.Equal(42, result.GetProperty("importance").GetInt32());
    }

    [Fact]
    public void UpcasterRegistry_RejectsMissingOrDuplicateUpcasters()
    {
        var eventType = new EventType("customer.created");
        var registry = new EventUpcasterRegistry();

        using var document = JsonDocument.Parse("""{"name":"Alice"}""");

        var missing = Assert.Throws<EventSerializationException>(
            () => registry.UpcastTo(
                eventType,
                new EventPayloadVersion(1),
                new EventPayloadVersion(2),
                document.RootElement));

        Assert.Equal("event.schema.upcaster-missing", missing.Error.Code);

        registry.Register(new CustomerCreatedV1ToV2Upcaster(eventType));

        Assert.Throws<InvalidOperationException>(
            () => registry.Register(new CustomerCreatedV1ToV2Upcaster(eventType)));
    }

    [Fact]
    public void UpcasterRegistry_RejectsNonSequentialRegistration()
    {
        var registry = new EventUpcasterRegistry();

        var exception = Assert.Throws<ArgumentException>(
            () => registry.Register(new InvalidUpcaster(
                new EventType("customer.created"),
                new EventPayloadVersion(1),
                new EventPayloadVersion(3))));

        Assert.Contains("exactly one", exception.Message);
    }

    private sealed record CustomerCreated(string Name, int Age);

    private sealed class CustomerCreatedV1ToV2Upcaster : IEventUpcaster
    {
        public CustomerCreatedV1ToV2Upcaster(EventType eventType)
        {
            EventType = eventType;
        }

        public EventType EventType { get; }

        public EventPayloadVersion FromVersion => new(1);

        public EventPayloadVersion ToVersion => new(2);

        public JsonElement Upcast(JsonElement payload)
        {
            using var document = JsonDocument.Parse($$"""{"name":{{JsonSerializer.Serialize(payload.GetProperty("name").GetString())}},"kind":"customer"}""");
            return document.RootElement.Clone();
        }
    }

    private sealed class CustomerCreatedV2ToV3Upcaster : IEventUpcaster
    {
        public CustomerCreatedV2ToV3Upcaster(EventType eventType)
        {
            EventType = eventType;
        }

        public EventType EventType { get; }

        public EventPayloadVersion FromVersion => new(2);

        public EventPayloadVersion ToVersion => new(3);

        public JsonElement Upcast(JsonElement payload)
        {
            using var document = JsonDocument.Parse("""{"name":"Alice","kind":"customer","importance":42}""");
            return document.RootElement.Clone();
        }
    }

    private sealed class InvalidUpcaster : IEventUpcaster
    {
        public InvalidUpcaster(
            EventType eventType,
            EventPayloadVersion fromVersion,
            EventPayloadVersion toVersion)
        {
            EventType = eventType;
            FromVersion = fromVersion;
            ToVersion = toVersion;
        }

        public EventType EventType { get; }

        public EventPayloadVersion FromVersion { get; }

        public EventPayloadVersion ToVersion { get; }

        public JsonElement Upcast(JsonElement payload) => payload.Clone();
    }
}
