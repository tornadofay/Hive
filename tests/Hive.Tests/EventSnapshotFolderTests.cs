using System.Text.Json;
using Hive.Core;
using Hive.Tests.TestInfrastructure;
using Xunit;

namespace Hive.Tests;

public sealed class EventSnapshotFolderTests
{
    [Fact]
    public void Fold_UpcastsAndAppliesEventsDeterministically()
    {
        var eventType = new EventType("counter.incremented");
        var upcasters = new EventUpcasterRegistry();

        upcasters.Register(new IncrementV1ToV2Upcaster(eventType));

        var serializer = new JsonEventSerializer(upcasters);
        var reducers = new EventStateReducerRegistry<int>();
        reducers.Register(new IncrementReducer(eventType));

        var folder = new EventSnapshotFolder<int>(reducers, serializer);

        var first = serializer.CreateEnvelope(
            EventId.New(),
            EventTestData.Timestamp,
            eventType,
            new EventPayloadVersion(1),
            CorrelationId.New(),
            null,
            new IncrementV1(2));

        var second = serializer.CreateEnvelope(
            EventId.New(),
            EventTestData.Timestamp.AddMinutes(1),
            eventType,
            new EventPayloadVersion(2),
            CorrelationId.New(),
            new CausationId(first.EventId.Value),
            new IncrementV2(3));

        var events = new[] { first, second };

        var firstFold = folder.Fold(0, events);
        var secondFold = folder.Fold(0, events);

        Assert.True(firstFold.IsSuccess, firstFold.Error?.Message);
        Assert.True(secondFold.IsSuccess, secondFold.Error?.Message);
        Assert.Equal(5, firstFold.Value);
        Assert.Equal(firstFold.Value, secondFold.Value);
    }

    [Fact]
    public void Fold_MissingReducerReturnsUnsupported()
    {
        var serializer = new JsonEventSerializer();
        var envelope = serializer.CreateEnvelope(
            EventId.New(),
            EventTestData.Timestamp,
            new EventType("unsupported.event"),
            new EventPayloadVersion(1),
            CorrelationId.New(),
            null,
            new { value = 1 });

        var folder = new EventSnapshotFolder<int>(
            new EventStateReducerRegistry<int>(),
            serializer);

        var result = folder.Fold(0, new[] { envelope });

        Assert.True(result.IsFailure);
        Assert.Equal("event.reducer.missing", result.Error?.Code);
        Assert.Equal(ErrorCategory.Unsupported, result.Error?.Category);
    }

    [Fact]
    public void Fold_FuturePayloadReturnsSerializationFailure()
    {
        var eventType = new EventType("counter.incremented");
        var serializer = new JsonEventSerializer();
        var reducers = new EventStateReducerRegistry<int>();
        reducers.Register(new IncrementReducer(eventType));

        var folder = new EventSnapshotFolder<int>(reducers, serializer);

        var envelope = serializer.CreateEnvelope(
            EventId.New(),
            EventTestData.Timestamp,
            eventType,
            new EventPayloadVersion(3),
            CorrelationId.New(),
            null,
            new { amount = 1 });

        var result = folder.Fold(0, new[] { envelope });

        Assert.True(result.IsFailure);
        Assert.Equal("event.schema.future", result.Error?.Code);
        Assert.Equal(ErrorCategory.Serialization, result.Error?.Category);
    }

    [Fact]
    public void ReducerRegistry_RejectsDuplicateEventTypes()
    {
        var eventType = new EventType("counter.incremented");
        var registry = new EventStateReducerRegistry<int>();
        var reducer = new IncrementReducer(eventType);

        registry.Register(reducer);

        Assert.Throws<InvalidOperationException>(
            () => registry.Register(reducer));
    }

    private sealed record IncrementV1(int Delta);

    private sealed record IncrementV2(int Amount);

    private sealed class IncrementV1ToV2Upcaster : IEventUpcaster
    {
        public IncrementV1ToV2Upcaster(EventType eventType)
        {
            EventType = eventType;
        }

        public EventType EventType { get; }

        public EventPayloadVersion FromVersion => new(1);

        public EventPayloadVersion ToVersion => new(2);

        public JsonElement Upcast(JsonElement payload)
        {
            var delta = payload.GetProperty("delta").GetInt32();

            using var document = JsonDocument.Parse(
                $$"""{"amount":{{delta}}}""");

            return document.RootElement.Clone();
        }
    }

    private sealed class IncrementReducer : IEventStateReducer<int>
    {
        public IncrementReducer(EventType eventType)
        {
            EventType = eventType;
        }

        public EventType EventType { get; }

        public EventPayloadVersion CurrentPayloadSchemaVersion => new(2);

        public int Apply(
            int state,
            EventEnvelope envelope,
            JsonElement payload) =>
            state + payload.GetProperty("amount").GetInt32();
    }
}
