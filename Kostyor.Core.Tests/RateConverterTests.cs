using Kostyor.Core.Money;
using Xunit;

namespace Kostyor.Core.Tests;

public class RateConverterTests
{
    private static RateConverter Make(params (string code, decimal value, int nominal)[] rates)
    {
        var dict = rates.ToDictionary(
            r => r.code,
            r => new CurrencyRate(r.value, r.nominal),
            StringComparer.OrdinalIgnoreCase);
        return new RateConverter(dict);
    }

    [Fact]
    public void Converts_by_division_nominal_one()
    {
        var conv = Make(("USD", 90m, 1));
        // 900 ₽ ÷ (90/1) = 10 $
        Assert.Equal(10m, conv.Convert(900m, "USD"));
    }

    [Fact]
    public void Honours_nominal_greater_than_one()
    {
        // CNY исторически бывал Nominal=10: 12.5 ₽ за 10 юаней → цена юаня 1.25 ₽.
        var conv = Make(("CNY", 12.5m, 10));
        // 125 ₽ ÷ (12.5/10) = 125 / 1.25 = 100 ¥
        Assert.Equal(100m, conv.Convert(125m, "CNY"));
    }

    [Fact]
    public void Nominal_matters_wrong_ignoring_it_would_differ()
    {
        var withNominal = Make(("CNY", 12.5m, 10));
        var naiveIgnoring = Make(("CNY", 12.5m, 1));
        // Если проигнорировать Nominal (взять 1), результат в 10 раз меньше — это и есть баг из AGENTS §12.
        Assert.NotEqual(withNominal.Convert(125m, "CNY"), naiveIgnoring.Convert(125m, "CNY"));
        Assert.Equal(10m, naiveIgnoring.Convert(125m, "CNY")); // 125 / 12.5 = 10
    }

    [Fact]
    public void Missing_currency_returns_null()
    {
        var conv = Make(("USD", 90m, 1));
        Assert.Null(conv.Convert(1000m, "JPY"));
    }

    [Fact]
    public void Invalid_rate_returns_null()
    {
        var conv = Make(("BAD", 0m, 1));
        Assert.Null(conv.Convert(1000m, "BAD"));
    }

    [Fact]
    public void Zero_nominal_returns_null_not_throws()
    {
        var conv = Make(("BAD", 90m, 0));
        Assert.Null(conv.Convert(1000m, "BAD"));
    }

    [Fact]
    public void Case_insensitive_code()
    {
        var conv = Make(("USD", 90m, 1));
        Assert.Equal(conv.Convert(900m, "USD"), conv.Convert(900m, "usd"));
    }
}
