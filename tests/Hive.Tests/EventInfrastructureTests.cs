using Hive.Core;
using Hive.Tests.TestInfrastructure;
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
    public void EventEnvelope_RejectsDefaultIdentityAndUndefinedPayload()
    {
        using var document = JsonDocument.Parse("""{"name":"Alice"}""");

        Assert.Throws<ArgumentException>(
            () => EventEnvelope.Create(
                default,
                EventTestData.Timestamp,
                new EventType("customer.created"),
                new EventPayloadVersion(1),
                CorrelationId.New(),
                null,
                document.RootElement));

        Assert.Throws<ArgumentException>(
            () => EventEnvelope.Create(
                EventId.New(),
                EventTestData.Timestamp,
                new EventType("customer.created"),
                new EventPayloadVersion(1),
                default,
                null,
                document.RootElement));

        Assert.Throws<ArgumentException>(
            () => EventEnvelope.Create(
                EventId.New(),
                EventTestData.Timestamp,
                new EventType("customer.created"),
                new EventPayloadVersion(1),
                CorrelationId.New(),
                (CausationId?)default(CausationId),
                document.RootElement));

        Assert.Throws<ArgumentException>(
            () => EventEnvelope.Create(
                EventId.New(),
                EventTestData.Timestamp,
                default(EventType),
                new EventPayloadVersion(1),
                CorrelationId.New(),
                null,
                document.RootElement));

        Assert.Throws<ArgumentException>(
            () => EventEnvelope.Create(
                EventId.New(),
                EventTestData.Timestamp,
                new EventType("customer.created"),
                default(EventPayloadVersion),
                CorrelationId.New(),
                null,
                document.RootElement));

        Assert.Throws<ArgumentException>(
            () => EventEnvelope.Create(
                EventId.New(),
                EventTestData.Timestamp,
                new EventType("customer.created"),
                new EventPayloadVersion(1),
                CorrelationId.New(),
                null,
                default(JsonElement)));
    }

    [Fact]
    public void UpcasterRegistry_NormalizesUpcasterFailures()
    {
        var eventType = new EventType("customer.created");
        var registry = new EventUpcasterRegistry();
        registry.Register(new ThrowingUpcaster(eventType));

        using var document = JsonDocument.Parse("""{"name":"Alice"}""");

        var exception = Assert.Throws<EventSerializationException>(
            () => registry.UpcastTo(
                eventType,
                new EventPayloadVersion(1),
                new EventPayloadVersion(2),
                document.RootElement));

        Assert.Equal(
            "event.schema.upcaster-failed",
            exception.Error.Code);
        Assert.Equal(
            ErrorCategory.Serialization,
            exception.Error.Category);
        Assert.DoesNotContain("simulated upcaster failure", exception.Error.Message, StringComparison.Ordinal);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public void UpcasterRegistry_RejectsUndefinedUpcasterPayload()
    {
        var eventType = new EventType("customer.created");
        var registry = new EventUpcasterRegistry();
        registry.Register(new UndefinedPayloadUpcaster(eventType));

        using var document = JsonDocument.Parse("""{"name":"Alice"}""");

        var exception = Assert.Throws<EventSerializationException>(
            () => registry.UpcastTo(
                eventType,
                new EventPayloadVersion(1),
                new EventPayloadVersion(2),
                document.RootElement));

        Assert.Equal(
            "event.schema.upcaster-invalid-payload",
            exception.Error.Code);
    }

    [Fact]
    public void EventPayloadVersion_RejectsNonPositiveValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventPayloadVersion(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventPayloadVersion(-1));
    }

    [Fact]
    public void EventType_TrimsRejectsBlankValuesAndHonorsPersistenceLength()
    {
        Assert.Equal("customer.created", new EventType("  customer.created  ").Value);
        Assert.Throws<ArgumentException>(() => new EventType(" "));

        var maxLength = new EventType(new string('x', 200));
        Assert.Equal(200, maxLength.Value.Length);

        Assert.Throws<ArgumentException>(
            () => new EventType(new string('x', 201)));
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
        Assert.Equal("Event envelope JSON is invalid.", malformed.Error.Message);
        Assert.NotNull(malformed.InnerException);
        Assert.DoesNotContain(
            malformed.InnerException!.Message,
            malformed.Error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Serializer_DoesNotExposeJsonExceptionTextForInvalidPayload()
    {
        var serializer = new JsonEventSerializer();

        var envelope = serializer.CreateEnvelope(
            EventId.New(),
            EventTestData.Timestamp,
            new EventType("customer.created"),
            new EventPayloadVersion(1),
            CorrelationId.New(),
            null,
            new
            {
                name = "Alice"
            });

        var exception = Assert.Throws<EventSerializationException>(
            () => serializer.DeserializePayload<int>(
                envelope,
                new EventPayloadVersion(1)));

        Assert.Equal("event.payload.invalid", exception.Error.Code);
        Assert.Equal(
            ErrorCategory.Serialization,
            exception.Error.Category);
        Assert.Equal(
            "Event 'customer.created' payload is invalid.",
            exception.Error.Message);
        Assert.NotNull(exception.InnerException);
        Assert.DoesNotContain(
            exception.InnerException!.Message,
            exception.Error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Serializer_RejectsFuturePayloadVersion()
    {
        var serializer = new JsonEventSerializer();

        var envelope = serializer.CreateEnvelope(
            EventId.New(),
            EventTestData.Timestamp,
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

        var invalidVersion = Assert.Throws<EventSerializationException>(
            () => serializer.DeserializePayload<CustomerCreated>(
                envelope,
                default));

        Assert.Equal("event.schema.version-invalid", invalidVersion.Error.Code);
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
    public void SnapshotFolder_ReducesFailuresWithoutLeakingExceptionText()
    {
        var registry = new EventStateReducerRegistry<int>();
        var eventType = new EventType("customer.created");
        registry.Register(new ThrowingReducer(eventType));

        var serializer = new JsonEventSerializer();
        var envelope = serializer.CreateEnvelope(
            EventId.New(),
            EventTestData.Timestamp,
            eventType,
            new EventPayloadVersion(1),
            CorrelationId.New(),
            null,
            new CustomerCreated("Alice", 42));

        var result = new EventSnapshotFolder<int>(
            registry,
            serializer).Fold(0, [envelope]);

        Assert.True(result.IsFailure);
        Assert.Equal("event.reducer.failed", result.Error!.Code);
        Assert.DoesNotContain("secret payload", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SnapshotFolder_PreservesReducerCancellation()
    {
        var registry = new EventStateReducerRegistry<int>();
        var eventType = new EventType("customer.cancelled");
        registry.Register(new CancellingReducer(eventType));

        var serializer = new JsonEventSerializer();
        var envelope = serializer.CreateEnvelope(
            EventId.New(),
            EventTestData.Timestamp,
            eventType,
            new EventPayloadVersion(1),
            CorrelationId.New(),
            null,
            new CustomerCreated("Alice", 42));

        Assert.Throws<OperationCanceledException>(
            () => new EventSnapshotFolder<int>(
                registry,
                serializer).Fold(0, [envelope]));
    }

    [Fact]
    public void ReducerRegistry_RejectsDefaultEventTypeOrVersion()
    {
        var registry = new EventStateReducerRegistry<int>();
        var validEventType = new EventType("customer.created");

        Assert.Throws<ArgumentException>(
            () => registry.Register(new InvalidReducer(
                default,
                new EventPayloadVersion(1))));

        Assert.Throws<ArgumentException>(
            () => registry.Register(new InvalidReducer(
                validEventType,
                default)));
    }

    [Fact]
    public void UpcasterRegistry_RejectsNonSequentialRegistration()
    {
        var registry = new EventUpcasterRegistry();
        var eventType = new EventType("customer.created");

        Assert.Throws<ArgumentException>(
            () => registry.Register(new InvalidUpcaster(
                default,
                new EventPayloadVersion(1),
                new EventPayloadVersion(2))));

        Assert.Throws<ArgumentException>(
            () => registry.Register(new InvalidUpcaster(
                eventType,
                default,
                new EventPayloadVersion(1))));

        var exception = Assert.Throws<ArgumentException>(
            () => registry.Register(new InvalidUpcaster(
                eventType,
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

    private sealed class ThrowingUpcaster : IEventUpcaster
    {
        public ThrowingUpcaster(EventType eventType)
        {
            EventType = eventType;
        }

        public EventType EventType { get; }

        public EventPayloadVersion FromVersion => new(1);

        public EventPayloadVersion ToVersion => new(2);

        public JsonElement Upcast(JsonElement payload) =>
            throw new InvalidOperationException("simulated upcaster failure");
    }

    private sealed class UndefinedPayloadUpcaster : IEventUpcaster
    {
        public UndefinedPayloadUpcaster(EventType eventType)
        {
            EventType = eventType;
        }

        public EventType EventType { get; }

        public EventPayloadVersion FromVersion => new(1);

        public EventPayloadVersion ToVersion => new(2);

        public JsonElement Upcast(JsonElement payload) =>
            default;
    }

    private sealed class ThrowingReducer : IEventStateReducer<int>
    {
        public ThrowingReducer(EventType eventType)
        {
            EventType = eventType;
        }

        public EventType EventType { get; }

        public EventPayloadVersion CurrentPayloadSchemaVersion => new(1);

        public int Apply(
            int state,
            EventEnvelope envelope,
            JsonElement payload) =>
            throw new InvalidOperationException("secret payload");
    }

    private sealed class CancellingReducer : IEventStateReducer<int>
    {
        public CancellingReducer(EventType eventType)
        {
            EventType = eventType;
        }

        public EventType EventType { get; }

        public EventPayloadVersion CurrentPayloadSchemaVersion => new(1);

        public int Apply(
            int state,
            EventEnvelope envelope,
            JsonElement payload) =>
            throw new OperationCanceledException();
    }

    private sealed class InvalidReducer : IEventStateReducer<int>
    {
        public InvalidReducer(
            EventType eventType,
            EventPayloadVersion currentVersion)
        {
            EventType = eventType;
            CurrentPayloadSchemaVersion = currentVersion;
        }

        public EventType EventType { get; }

        public EventPayloadVersion CurrentPayloadSchemaVersion { get; }

        public int Apply(
            int state,
            EventEnvelope envelope,
            JsonElement payload) =>
            state;
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
