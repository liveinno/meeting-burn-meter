using System.Globalization;

namespace Kostyor.Core.Formatting;

/// <summary>
/// Форматирование чисел, денег и времени по <c>ru-RU</c> (AGENTS §9, ТЗ §5).
/// Разделитель разрядов — неразрывный пробел культуры ru-RU. Все методы — чистые.
/// </summary>
public static class RuFormat
{
    /// <summary>Неразрывный пробел (U+00A0) — разделитель разрядов (ТЗ §5, AGENTS §9).</summary>
    public const string GroupSeparator = " ";

    /// <summary>Культура ru-RU для дат/прочего форматирования.</summary>
    public static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    /// <summary>
    /// Числовой формат на базе ru-RU, но с явно зафиксированным неразрывным пробелом-разделителем —
    /// не зависим от версии ICU (у разных ICU ru-RU даёт то U+00A0, то U+202F). Совпадает с ТЗ.
    /// </summary>
    private static readonly NumberFormatInfo RuNumbers = BuildRuNumbers();

    private static NumberFormatInfo BuildRuNumbers()
    {
        var nfi = (NumberFormatInfo)Ru.NumberFormat.Clone();
        nfi.NumberGroupSeparator = GroupSeparator;
        nfi.CurrencyGroupSeparator = GroupSeparator;
        nfi.PercentGroupSeparator = GroupSeparator;
        nfi.NumberDecimalSeparator = ",";
        return nfi;
    }

    /// <summary>Целочисленная сумма без копеек: <c>1 234 567</c> (одометр показывает целые ₽).</summary>
    public static string Money(decimal amount)
        => Math.Floor(amount).ToString("#,0", RuNumbers);

    /// <summary>Сумма с фиксированным числом знаков после запятой (для отчётов).</summary>
    public static string Money(decimal amount, int decimals)
        => amount.ToString("#,0." + new string('0', Math.Max(0, decimals)), RuNumbers);

    /// <summary>Число с группировкой разрядов по ru-RU (неразрывный пробел).</summary>
    public static string Number(decimal value)
        => value.ToString("#,0", RuNumbers);

    /// <summary>
    /// Время встречи. При длительности ≥ 1 ч — <c>Ч:ММ:СС</c> (часы не переполняют минуты),
    /// иначе <c>ММ:СС</c> (AGENTS §12, ТЗ §1). Часы не оборачиваются на 24 ч.
    /// </summary>
    public static string Time(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        var totalHours = (int)t.TotalHours;
        return totalHours >= 1
            ? $"{totalHours}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{t.Minutes:00}:{t.Seconds:00}";
    }

    /// <summary>Скорость сжигания «−N ₽/мин» (знак минус как в дизайне).</summary>
    public static string BurnPerMinute(decimal ratePerHour)
        => "−" + Number(Math.Round(ratePerHour / 60m, MidpointRounding.AwayFromZero)) + " ₽/мин";

    /// <summary>
    /// Русская форма множественного числа: <paramref name="one"/> (1 файл),
    /// <paramref name="few"/> (2–4 файла), <paramref name="many"/> (5 файлов).
    /// </summary>
    public static string Plural(long n, string one, string few, string many)
    {
        n = Math.Abs(n);
        var m10 = n % 10;
        var m100 = n % 100;
        if (m10 == 1 && m100 != 11) return one;
        if (m10 >= 2 && m10 <= 4 && (m100 < 10 || m100 >= 20)) return few;
        return many;
    }

    /// <summary>«N человек · X ₽/час» для шапки панели участников (как в дизайне).</summary>
    public static string HeadcountLine(int headcount, decimal ratePerHour)
        => $"{headcount} {Plural(headcount, "человек", "человека", "человек")} · {Number(ratePerHour)} ₽/час";
}
