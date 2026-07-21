using System.Globalization;
using Kostyor.Core.Formatting;
using Xunit;

namespace Kostyor.Core.Tests;

public class RuFormatTests
{
    private const string NB = " ";      // неразрывный пробел — разделитель разрядов
    private const string MINUS = "−";   // математический минус (как в дизайне)
    private const string MIDDOT = "·";  // средняя точка-разделитель

    [Fact]
    public void Money_groups_thousands_with_non_breaking_space()
    {
        var s = RuFormat.Money(1234567m);
        Assert.Equal($"1{NB}234{NB}567", s);

        var sep = RuFormat.GroupSeparator[0];
        Assert.Equal(' ', sep);
        Assert.NotEqual(' ', sep);      // не обычный ASCII-пробел (ТЗ §5)
        Assert.True(char.IsWhiteSpace(sep));
    }

    [Fact]
    public void Money_floors_kopecks()
    {
        Assert.Equal("999", RuFormat.Money(999.99m));
        Assert.Equal("0", RuFormat.Money(0.4m));
    }

    [Fact]
    public void Money_with_decimals_uses_comma()
    {
        Assert.Equal($"1{NB}000,50", RuFormat.Money(1000.5m, 2));
    }

    [Fact]
    public void Time_under_hour_is_mm_ss()
    {
        Assert.Equal("00:00", RuFormat.Time(TimeSpan.Zero));
        Assert.Equal("05:09", RuFormat.Time(new TimeSpan(0, 5, 9)));
        Assert.Equal("59:59", RuFormat.Time(new TimeSpan(0, 59, 59)));
    }

    [Fact]
    public void Time_at_or_over_hour_is_h_mm_ss()
    {
        Assert.Equal("1:00:00", RuFormat.Time(new TimeSpan(1, 0, 0)));
        Assert.Equal("1:05:03", RuFormat.Time(new TimeSpan(1, 5, 3)));
        Assert.Equal("2:07:42", RuFormat.Time(new TimeSpan(2, 7, 42)));
    }

    [Fact]
    public void Time_beyond_24h_does_not_wrap()
    {
        Assert.Equal("25:00:01", RuFormat.Time(new TimeSpan(1, 1, 0, 1)));
    }

    [Theory]
    [InlineData(1, "человек")]
    [InlineData(2, "человека")]
    [InlineData(4, "человека")]
    [InlineData(5, "человек")]
    [InlineData(11, "человек")]
    [InlineData(21, "человек")]
    [InlineData(22, "человека")]
    [InlineData(25, "человек")]
    public void Plural_russian_rules(long n, string expected)
        => Assert.Equal(expected, RuFormat.Plural(n, "человек", "человека", "человек"));

    [Fact]
    public void BurnPerMinute_formats_with_minus()
    {
        // 60000 ₽/час → 1000 ₽/мин
        Assert.Equal($"{MINUS}1{NB}000 ₽/мин", RuFormat.BurnPerMinute(60000m));
    }

    [Fact]
    public void HeadcountLine_matches_design_shape()
    {
        Assert.Equal($"4 человека {MIDDOT} 6{NB}000 ₽/час", RuFormat.HeadcountLine(4, 6000m));
    }

    [Fact]
    public void Ru_culture_is_available()
    {
        // Убеждаемся, что ru-RU реально доступна в рантайме (ICU/InvariantGlobalization off).
        Assert.Equal("ru-RU", RuFormat.Ru.Name);
        Assert.NotEqual(CultureInfo.InvariantCulture, RuFormat.Ru);
    }
}
