using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Controls;
using Xunit;

namespace Hive.Tests;

public sealed class HiveScrollBarTests
{
    [Fact]
    public void State_DerivesEffectiveMaximumFromContentExtentAndViewport()
    {
        var state = HiveScrollState.Create(
            Orientation.Vertical,
            minimum: 0,
            maximum: 1000,
            value: 250,
            viewportSize: 200,
            smallChange: 10,
            largeChange: 150);

        Assert.Equal(800, state.EffectiveMaximum);
        Assert.Equal(1000, state.ContentExtent);
        Assert.True(state.CanScroll);
        Assert.Equal(250, state.Value);
    }

    [Fact]
    public void State_ClampsValueToEffectiveMaximum()
    {
        var state = HiveScrollState.Create(
            Orientation.Horizontal,
            minimum: 0,
            maximum: 500,
            value: 999,
            viewportSize: 200);

        Assert.Equal(300, state.EffectiveMaximum);
        Assert.Equal(300, state.Value);
    }

    [Fact]
    public void State_RejectsInvalidRangeAndChangeValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => HiveScrollState.Create(
                Orientation.Vertical,
                minimum: 20,
                maximum: 10,
                value: 20,
                viewportSize: 10));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => HiveScrollState.Create(
                Orientation.Vertical,
                minimum: 0,
                maximum: 10,
                value: 0,
                viewportSize: -1));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => HiveScrollState.Create(
                Orientation.Vertical,
                minimum: 0,
                maximum: 10,
                value: 0,
                viewportSize: 1,
                smallChange: -1));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => HiveScrollState.Create(
                Orientation.Vertical,
                minimum: 0,
                maximum: 10,
                value: 0,
                viewportSize: 1,
                largeChange: -1));
    }

    [Fact]
    public void Thumb_UsesMinimumWhenProportionalThumbWouldBeTooSmall()
    {
        using var scrollbar = new HiveScrollBar
        {
            Size = new Size(20, 100)
        };

        scrollbar.SetState(
            HiveScrollState.Create(
                Orientation.Vertical,
                minimum: 0,
                maximum: 1000,
                value: 0,
                viewportSize: 100,
                enabled: true));

        var thumb = scrollbar.GetThumbBoundsForTesting();
        var track = scrollbar.GetTrackBoundsForTesting();

        Assert.Equal(Math.Min(20, track.Height), thumb.Height);
    }

    [Fact]
    public void Thumb_GrowsProportionallyAsViewportGrows()
    {
        using var scrollbar = new HiveScrollBar
        {
            Size = new Size(20, 100)
        };

        scrollbar.SetState(
            HiveScrollState.Create(
                Orientation.Vertical,
                minimum: 0,
                maximum: 200,
                value: 0,
                viewportSize: 100,
                enabled: true));

        var largeViewportThumb = scrollbar.GetThumbBoundsForTesting().Height;

        scrollbar.SetState(
            HiveScrollState.Create(
                Orientation.Vertical,
                minimum: 0,
                maximum: 200,
                value: 0,
                viewportSize: 40,
                enabled: true));

        var smallViewportThumb = scrollbar.GetThumbBoundsForTesting().Height;

        Assert.True(largeViewportThumb > smallViewportThumb);
    }

    [Fact]
    public void DragMapping_ReachesBothEffectiveEndpoints()
    {
        Assert.Equal(
            0,
            HiveScrollMetrics.CalculateValueFromThumbPosition(
                thumbStart: 0,
                trackStart: 0,
                trackLength: 100,
                thumbLength: 20,
                minimum: 0,
                maximum: 80));

        Assert.Equal(
            80,
            HiveScrollMetrics.CalculateValueFromThumbPosition(
                thumbStart: 80,
                trackStart: 0,
                trackLength: 100,
                thumbLength: 20,
                minimum: 0,
                maximum: 80));
    }

    [Fact]
    public void DragMapping_InterpolatesValueAcrossThumbTravel()
    {
        var value = HiveScrollMetrics.CalculateValueFromThumbPosition(
            thumbStart: 40,
            trackStart: 0,
            trackLength: 100,
            thumbLength: 20,
            minimum: 10,
            maximum: 90);

        Assert.Equal(50, value);
    }

    [Fact]
    public void TrackPaging_UsesLargeChangeAndClampsToBounds()
    {
        Assert.Equal(
            50,
            HiveScrollMetrics.CalculatePageTarget(
                current: 0,
                minimum: 0,
                effectiveMaximum: 200,
                largeChange: 50,
                forward: true));

        Assert.Equal(
            200,
            HiveScrollMetrics.CalculatePageTarget(
                current: 175,
                minimum: 0,
                effectiveMaximum: 200,
                largeChange: 50,
                forward: true));

        Assert.Equal(
            0,
            HiveScrollMetrics.CalculatePageTarget(
                current: 20,
                minimum: 0,
                effectiveMaximum: 200,
                largeChange: 50,
                forward: false));
    }

    [Fact]
    public void SetValue_RaisesOneChangeNotificationPerActualValueChange()
    {
        using var scrollbar = new HiveScrollBar();
        scrollbar.SetState(
            HiveScrollState.Create(
                Orientation.Vertical,
                minimum: 0,
                maximum: 500,
                value: 0,
                viewportSize: 100,
                enabled: true));

        var changes = 0;
        scrollbar.ValueChanged += (_, _) => changes++;

        scrollbar.SetValue(25);
        scrollbar.SetValue(25);
        scrollbar.SetValue(100);

        Assert.Equal(2, changes);
        Assert.Equal(100, scrollbar.Value);
    }

    [Fact]
    public void DisabledState_DisablesControlAndRemovesScrollableThumb()
    {
        using var scrollbar = new HiveScrollBar();

        scrollbar.SetState(
            HiveScrollState.Create(
                Orientation.Vertical,
                minimum: 0,
                maximum: 500,
                value: 100,
                viewportSize: 100,
                enabled: false));

        Assert.False(scrollbar.Enabled);
        Assert.False(scrollbar.CanScroll);
        Assert.Empty(scrollbar.GetThumbBoundsForTesting());
    }
}
