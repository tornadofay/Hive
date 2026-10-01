namespace Hive.Host.WinForms.UI.Controls;

internal static class HiveScrollMetrics
{
    public static int CalculateThumbLength(
        int trackLength,
        int contentExtent,
        int viewportSize,
        int minimumThumbSize)
    {
        if (trackLength <= 0)
            return 0;

        if (contentExtent <= 0 ||
            viewportSize <= 0)
        {
            return trackLength;
        }

        var proportional =
            (int)Math.Round(
                trackLength *
                (viewportSize / (double)contentExtent),
                MidpointRounding.AwayFromZero);

        return Math.Clamp(
            proportional,
            Math.Min(
                Math.Max(1, minimumThumbSize),
                trackLength),
            trackLength);
    }

    public static int CalculateThumbPosition(
        int trackStart,
        int trackLength,
        int thumbLength,
        int minimum,
        int maximum,
        int value)
    {
        var effectiveMaximum = Math.Max(minimum, maximum);
        var range = effectiveMaximum - minimum;
        var travel = Math.Max(0, trackLength - thumbLength);

        if (range <= 0 || travel <= 0)
            return trackStart;

        var clampedValue = Math.Clamp(
            value,
            minimum,
            effectiveMaximum);

        var offset = (int)Math.Round(
            travel *
            ((clampedValue - minimum) / (double)range),
            MidpointRounding.AwayFromZero);

        return trackStart + offset;
    }

    public static int CalculateValueFromThumbPosition(
        int thumbStart,
        int trackStart,
        int trackLength,
        int thumbLength,
        int minimum,
        int maximum,
        int position)
    {
        var effectiveMaximum = Math.Max(minimum, maximum);
        var range = effectiveMaximum - minimum;
        var travel = Math.Max(0, trackLength - thumbLength);

        if (range <= 0 || travel <= 0)
            return minimum;

        var minStart = trackStart;
        var maxStart = trackStart + travel;

        var clampedStart = Math.Clamp(
            thumbStart,
            minStart,
            maxStart);

        var ratio = (clampedStart - minStart) /
                    (double)travel;

        return Math.Clamp(
            minimum +
            (int)Math.Round(
                ratio * range,
                MidpointRounding.AwayFromZero),
            minimum,
            effectiveMaximum);
    }

    public static int CalculatePageTarget(
        int current,
        int minimum,
        int effectiveMaximum,
        int largeChange,
        bool forward)
    {
        var step = Math.Max(
            1,
            largeChange);

        return forward
            ? Math.Min(
                effectiveMaximum,
                current + step)
            : Math.Max(
                minimum,
                current - step);
    }
}
