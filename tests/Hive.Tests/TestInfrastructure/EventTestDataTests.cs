using Hive.Core;
using Hive.Tests.TestInfrastructure;
using Xunit;

namespace Hive.Tests;

public sealed class EventTestDataTests
{
    [Fact]
    public void EventTestData_UsesStableUtcTimestamp()
    {
        var envelope = EventTestData.CreateEnvelope();

        Assert.Equal(
            new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero),
            envelope.OccurredAtUtc);
    }

    [Fact]
    public void EventTestData_CreatesExplicitPayloadVersion()
    {
        var envelope = EventTestData.CreateEnvelope("test.event", 2);

        Assert.Equal("test.event", envelope.EventType.Value);
        Assert.Equal(2, envelope.PayloadSchemaVersion.Value);
        Assert.Equal("test", envelope.Payload.GetProperty("value").GetString());
    }
}
