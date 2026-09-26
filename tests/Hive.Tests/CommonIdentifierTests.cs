using Hive.Core;
using Xunit;

namespace Hive.Tests;

public sealed class CommonIdentifierTests
{
    [Fact]
    public void EventId_RejectsEmptyGuid()
    {
        Assert.Throws<ArgumentException>(() => new EventId(Guid.Empty));
    }

    [Fact]
    public void CorrelationId_RejectsEmptyGuid()
    {
        Assert.Throws<ArgumentException>(() => new CorrelationId(Guid.Empty));
    }

    [Fact]
    public void CausationId_RejectsEmptyGuid()
    {
        Assert.Throws<ArgumentException>(() => new CausationId(Guid.Empty));
    }

    [Fact]
    public void Identifier_ParseAndTryParse_RoundTrip()
    {
        var value = Guid.NewGuid();

        var eventId = EventId.Parse(value.ToString("D"));
        var correlationId = CorrelationId.Parse(value.ToString("D"));
        var causationId = CausationId.Parse(value.ToString("D"));

        Assert.Equal(value, eventId.Value);
        Assert.Equal(value, correlationId.Value);
        Assert.Equal(value, causationId.Value);

        Assert.True(EventId.TryParse(eventId.ToString(), out var parsedEventId));
        Assert.True(CorrelationId.TryParse(correlationId.ToString(), out var parsedCorrelationId));
        Assert.True(CausationId.TryParse(causationId.ToString(), out var parsedCausationId));

        Assert.Equal(eventId, parsedEventId);
        Assert.Equal(correlationId, parsedCorrelationId);
        Assert.Equal(causationId, parsedCausationId);
    }

    [Fact]
    public void TryParse_RejectsInvalidOrEmptyValues()
    {
        Assert.False(EventId.TryParse("not-a-guid", out _));
        Assert.False(EventId.TryParse(Guid.Empty.ToString("D"), out _));
        Assert.False(CorrelationId.TryParse(null, out _));
        Assert.False(CausationId.TryParse(string.Empty, out _));
    }
}
