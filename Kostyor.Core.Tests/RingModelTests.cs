using Kostyor.Core.Ring;
using Xunit;

namespace Kostyor.Core.Tests;

public class RingModelTests
{
    [Theory]
    [InlineData(0, RingModel.Green)]
    [InlineData(14, RingModel.Green)]
    [InlineData(15, RingModel.Yellow)]
    [InlineData(29, RingModel.Yellow)]
    [InlineData(30, RingModel.Orange)]
    [InlineData(44, RingModel.Orange)]
    [InlineData(45, RingModel.Red)]
    [InlineData(90, RingModel.Red)]
    public void Color_follows_default_thresholds(int minutes, string expected)
    {
        var v = RingModel.Compute(TimeSpan.FromMinutes(minutes));
        Assert.Equal(expected, v.Color);
    }

    [Fact]
    public void IsRed_only_in_red_zone()
    {
        Assert.False(RingModel.Compute(TimeSpan.FromMinutes(44)).IsRed);
        Assert.True(RingModel.Compute(TimeSpan.FromMinutes(45)).IsRed);
    }

    [Fact]
    public void Progress_fills_over_fill_minutes_and_clamps()
    {
        Assert.Equal(0d, RingModel.Compute(TimeSpan.Zero, fillMinutes: 60).Progress, 5);
        Assert.Equal(0.5d, RingModel.Compute(TimeSpan.FromMinutes(30), fillMinutes: 60).Progress, 5);
        Assert.Equal(1d, RingModel.Compute(TimeSpan.FromMinutes(60), fillMinutes: 60).Progress, 5);
        Assert.Equal(1d, RingModel.Compute(TimeSpan.FromMinutes(200), fillMinutes: 60).Progress, 5); // clamp
    }

    [Fact]
    public void Custom_thresholds_are_respected()
    {
        var thresholds = new[] { 5, 10, 20 };
        Assert.Equal(RingModel.Green, RingModel.Compute(TimeSpan.FromMinutes(4), thresholds).Color);
        Assert.Equal(RingModel.Yellow, RingModel.Compute(TimeSpan.FromMinutes(5), thresholds).Color);
        Assert.Equal(RingModel.Orange, RingModel.Compute(TimeSpan.FromMinutes(10), thresholds).Color);
        Assert.Equal(RingModel.Red, RingModel.Compute(TimeSpan.FromMinutes(20), thresholds).Color);
    }
}
