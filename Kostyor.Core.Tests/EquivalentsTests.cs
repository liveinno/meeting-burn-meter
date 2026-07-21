using Kostyor.Core.Equivalents;
using Xunit;

namespace Kostyor.Core.Tests;

public class EquivalentsTests
{
    [Fact]
    public void Crossed_returns_only_newly_passed_milestones()
    {
        var crossed = EquivalentsCalc.Crossed(900m, 1200m, EquivalentsCalc.DefaultMilestones).ToList();
        Assert.Single(crossed);
        Assert.Equal(1000m, crossed[0].ThresholdRub);
    }

    [Fact]
    public void Crossed_returns_multiple_when_jumped()
    {
        var crossed = EquivalentsCalc.Crossed(0m, 11000m, EquivalentsCalc.DefaultMilestones).ToList();
        Assert.Equal(3, crossed.Count);
    }

    [Fact]
    public void Crossed_empty_when_already_past()
    {
        var crossed = EquivalentsCalc.Crossed(2000m, 3000m, EquivalentsCalc.DefaultMilestones).ToList();
        Assert.Empty(crossed);
    }

    [Fact]
    public void RunningLine_has_coffee_and_mrot()
    {
        var line = EquivalentsCalc.RunningLine(1000m, coffeePriceRub: 250m, mrotHourlyRub: 140m);
        Assert.Contains("4 чашки кофе", line);
        Assert.Contains("по МРОТ", line);
        Assert.StartsWith("= ", line);
    }

    [Fact]
    public void RunningLine_skips_disabled_parts()
    {
        var line = EquivalentsCalc.RunningLine(1000m, coffeePriceRub: 0m, mrotHourlyRub: 0m);
        Assert.Equal(string.Empty, line);
    }

    [Fact]
    public void SummaryEquivalents_pizza_only_below_5000()
    {
        var eq = EquivalentsCalc.SummaryEquivalents(3000m);
        Assert.Single(eq);
        Assert.Equal("🍕", eq[0].Icon);
        Assert.Contains("3", eq[0].Text); // 3 пиццы
    }

    [Fact]
    public void SummaryEquivalents_adds_phone_and_mac()
    {
        var eq = EquivalentsCalc.SummaryEquivalents(12000m);
        Assert.Equal(3, eq.Count);
        Assert.Equal("📱", eq[1].Icon);
        Assert.Equal("💻", eq[2].Icon);
        Assert.Contains("MacBook", eq[2].Text);
    }

    [Fact]
    public void SummaryEquivalents_uses_configured_prices()
    {
        // Пицца по 500 ₽ → на 2000 ₽ это 4 пиццы; смартфон по 2000 → 1 шт; ноут по 3000 → нет.
        var prices = new EquivalentPrices(PizzaRub: 500m, SmartphoneRub: 2000m, LaptopRub: 3000m);
        var eq = EquivalentsCalc.SummaryEquivalents(2000m, prices);

        Assert.Equal(2, eq.Count);
        Assert.Equal("🍕", eq[0].Icon);
        Assert.Contains("4", eq[0].Text);
        Assert.Equal("📱", eq[1].Icon);
        Assert.Contains("1", eq[1].Text);
    }

    [Fact]
    public void SummaryEquivalents_hides_items_with_zero_price()
    {
        // Цена 0 отключает эквивалент — остаётся только пицца.
        var prices = new EquivalentPrices(PizzaRub: 1000m, SmartphoneRub: 0m, LaptopRub: 0m);
        var eq = EquivalentsCalc.SummaryEquivalents(999999m, prices);

        Assert.Single(eq);
        Assert.Equal("🍕", eq[0].Icon);
    }

    [Fact]
    public void ManualRates_snapshot_uses_per_unit_with_nominal_one()
    {
        var cfg = new Kostyor.Core.Config.ManualRatesConfig { UsdPerUnit = 100m, EurPerUnit = 110m, CnyPerUnit = 14m };
        var snap = cfg.ToSnapshot(new System.DateTimeOffset(2026, 7, 18, 0, 0, 0, System.TimeSpan.Zero));

        Assert.Equal(Kostyor.Core.Money.RatesOrigin.Manual, snap.Origin);
        Assert.Equal(100m, snap.Get("USD")!.Value.PerUnit);
        Assert.Equal(1, snap.Get("USD")!.Value.Nominal);

        // Конвертер: 1000 ₽ ÷ 100 = 10 $.
        var conv = new Kostyor.Core.Money.RateConverter(snap);
        Assert.Equal(10m, conv.Convert(1000m, "USD"));
    }
}
