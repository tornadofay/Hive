using Hive.Core;

namespace Hive.Tests.TestInfrastructure;

internal sealed class FakeClock : IClock
{
    public FakeClock(DateTimeOffset utcNow)
    {
        UtcNow = utcNow.ToUniversalTime();
    }

    public DateTimeOffset UtcNow { get; private set; }

    public void Set(DateTimeOffset utcNow)
    {
        UtcNow = utcNow.ToUniversalTime();
    }

    public void Advance(TimeSpan amount)
    {
        if (amount < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Clock cannot move backwards.");

        UtcNow = UtcNow.Add(amount);
    }
}
