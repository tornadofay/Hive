using Hive.Core;
using System.Text.Json;

namespace Hive.Tests.TestInfrastructure;

internal static class EventTestData
{
    public static DateTimeOffset Timestamp =>
        new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

    public static EventEnvelope CreateEnvelope(
        string eventType = "test.event",
        int payloadVersion = 1,
        JsonElement? payload = null) =>
        EventEnvelope.Create(
            EventId.New(),
            Timestamp,
            new EventType(eventType),
            new EventPayloadVersion(payloadVersion),
            CorrelationId.New(),
            CausationId.New(),
            payload ?? CreatePayload());

    private static JsonElement CreatePayload()
    {
        using var document = JsonDocument.Parse("""{"value":"test"}""");
        return document.RootElement.Clone();
    }
}
