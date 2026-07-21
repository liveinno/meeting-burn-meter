using Kostyor.Core.Fire;
using Xunit;

namespace Kostyor.Core.Tests;

public class FireModelTests
{
    [Theory]
    [InlineData(999, 0)]
    [InlineData(1000, 1)]
    [InlineData(1999, 1)]
    [InlineData(2000, 2)]
    [InlineData(3000, 3)]
    [InlineData(5000, 3)]
    public void MoneyPhase_follows_thresholds(decimal rub, int expected)
    {
        Assert.Equal(expected, FireModel.MoneyPhase(rub, 1000m, 2000m, 3000m));
    }

    [Fact]
    public void Zero_threshold_disables_that_step()
    {
        // Оранжевый и красный пороги не заданы (0) — дальше жёлтой фазы не разгорается.
        Assert.Equal(1, FireModel.MoneyPhase(5000m, 1000m, 0m, 0m));
    }

    [Fact]
    public void Visual_phase0_is_all_zeros()
    {
        var v = FireModel.Visual(0);
        Assert.Equal(0d, v.EmberOpacity);
        Assert.Equal(0d, v.TongueOpacity);
        Assert.Equal(0d, v.BlazeOpacity);
        Assert.Equal(0d, v.HeatGlowOpacity);
        Assert.Equal(0d, v.ScrimOpacity);
    }

    [Fact]
    public void Visual_phase1_embers_only()
    {
        var v = FireModel.Visual(1);
        Assert.Equal(0.75d, v.EmberOpacity);
        Assert.Equal(0d, v.TongueOpacity);
    }

    [Fact]
    public void Visual_phase2_tongues_without_blaze()
    {
        var v = FireModel.Visual(2);
        Assert.Equal(1d, v.TongueOpacity);
        Assert.Equal(0d, v.BlazeOpacity);
    }

    [Fact]
    public void Visual_phase3_full_blaze_and_scrim()
    {
        var v = FireModel.Visual(3);
        Assert.Equal(1d, v.BlazeOpacity);
        Assert.Equal(0.55d, v.ScrimOpacity);
    }
}
