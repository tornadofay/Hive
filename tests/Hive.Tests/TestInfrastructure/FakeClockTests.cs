using Hive.Core;
using Hive.Tests.TestInfrastructure;
using Xunit;

namespace Hive.Tests;

public sealed class FakeClockTests
{
    [Fact]
    public void FakeClock_StartsAtProvidedUtcInstant()
    {
        var expected = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.FromHours(2));
        var clock = new FakeClock(expected);

        Assert.Equal(expected.ToUniversalTime(), clock.UtcNow);
    }

    [Fact]
    public void FakeClock_AdvancesDeterministically()
    {
        var clock = new FakeClock(
            new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero));

        clock.Advance(TimeSpan.FromMinutes(15));

        Assert.Equal(
            new DateTimeOffset(2030, 1, 2, 3, 19, 5, TimeSpan.Zero),
            clock.UtcNow);
    }

    [Fact]
    public void FakeClock_RejectsBackwardMovement()
    {
        var clock = new FakeClock(
            new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => clock.Advance(TimeSpan.FromSeconds(-1)));
    }
}
