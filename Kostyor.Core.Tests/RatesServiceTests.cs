using Kostyor.Core.Config;
using Kostyor.Core.Money;
using Xunit;

namespace Kostyor.Core.Tests;

/// <summary>Сеть убрана — сервис теперь просто держит локальный снимок курса и умеет его заменить.</summary>
public class RatesServiceTests
{
    private static RatesSnapshot Snap(decimal usd) =>
        new ManualRatesConfig { UsdPerUnit = usd }
            .ToSnapshot(new DateTimeOffset(2026, 7, 18, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Holds_initial_snapshot()
    {
        var svc = new RatesService(Snap(90m));
        Assert.Equal(RatesOrigin.Manual, svc.Current.Origin);
        Assert.Equal(90m, svc.Current.Get("USD")!.Value.PerUnit);
    }

    [Fact]
    public void Apply_replaces_current()
    {
        var svc = new RatesService(Snap(90m));
        svc.Apply(Snap(100m));
        Assert.Equal(100m, svc.Current.Get("USD")!.Value.PerUnit);
    }

    [Fact]
    public void Null_arguments_throw()
    {
        Assert.Throws<ArgumentNullException>(() => new RatesService(null!));
        var svc = new RatesService(Snap(90m));
        Assert.Throws<ArgumentNullException>(() => svc.Apply(null!));
    }
}
