using Kostyor.Core.Formatting;

namespace Kostyor.Core.Equivalents;

/// <summary>Веха-эквивалент: при достижении суммы — тост с иконкой (дизайн + ТЗ §3).</summary>
public readonly record struct Milestone(decimal ThresholdRub, string Text, string Icon);

/// <summary>
/// Настраиваемые цены «сожгли, как…» для карточки итога (ТЗ §3 «настраиваемо»).
/// Любая цена ≤ 0 скрывает свой эквивалент. Значения по умолчанию совпадают с дизайном.
/// </summary>
public readonly record struct EquivalentPrices(decimal PizzaRub, decimal SmartphoneRub, decimal LaptopRub)
{
    public static readonly EquivalentPrices Default = new(1000m, 5000m, 10000m);
}

/// <summary>
/// Живые эквиваленты сожжённых денег (ТЗ §3, §«Улучшения»). Пороговые вехи для тостов
/// и «сожгли, как N пицц / X ч по МРОТ» для карточки итога. Чистые функции.
/// </summary>
public static class EquivalentsCalc
{
    /// <summary>Вехи по умолчанию (совпадают с дизайном).</summary>
    public static readonly IReadOnlyList<Milestone> DefaultMilestones = new[]
    {
        new Milestone(1000m, "Сгорела пицца на команду", "🍕"),
        new Milestone(5000m, "Сгорел бюджет на смартфон", "📱"),
        new Milestone(10000m, "Полетел MacBook", "💻"),
    };

    /// <summary>Вехи, только что пересечённые при переходе суммы <paramref name="prevRub"/> → <paramref name="curRub"/>.</summary>
    public static IEnumerable<Milestone> Crossed(decimal prevRub, decimal curRub, IReadOnlyList<Milestone> milestones)
    {
        foreach (var m in milestones)
            if (curRub >= m.ThresholdRub && prevRub < m.ThresholdRub)
                yield return m;
    }

    /// <summary>
    /// Живая строка эквивалентов «= N чашек кофе · X ч по МРОТ» (ТЗ §3, настраиваемо).
    /// </summary>
    public static string RunningLine(decimal rub, decimal coffeePriceRub, decimal mrotHourlyRub)
    {
        var parts = new List<string>(2);
        if (coffeePriceRub > 0m)
        {
            var cups = (long)Math.Floor(rub / coffeePriceRub);
            parts.Add($"{cups} {RuFormat.Plural(cups, "чашка", "чашки", "чашек")} кофе");
        }
        if (mrotHourlyRub > 0m)
        {
            var hours = rub / mrotHourlyRub;
            parts.Add($"{hours.ToString("0.#", RuFormat.Ru)} ч по МРОТ");
        }
        return parts.Count == 0 ? string.Empty : "= " + string.Join(" · ", parts);
    }

    /// <summary>Строка «Сожгли, как» с ценами по умолчанию (пиццы/смартфоны/макбуки — как в дизайне).</summary>
    public static IReadOnlyList<(string Icon, string Text)> SummaryEquivalents(decimal rub)
        => SummaryEquivalents(rub, EquivalentPrices.Default);

    /// <summary>
    /// Строка «Сожгли, как» для карточки итога с настраиваемыми ценами (ТЗ §3).
    /// Пицца показывается всегда (если цена &gt; 0); смартфон/ноутбук — при достижении их цены.
    /// </summary>
    public static IReadOnlyList<(string Icon, string Text)> SummaryEquivalents(decimal rub, EquivalentPrices prices)
    {
        var list = new List<(string, string)>();
        if (prices.PizzaRub > 0m)
        {
            var pizza = (long)Math.Floor(rub / prices.PizzaRub);
            list.Add(("🍕", $"{pizza} {RuFormat.Plural(pizza, "пицца", "пиццы", "пицц")}"));
        }
        if (prices.SmartphoneRub > 0m && rub >= prices.SmartphoneRub)
        {
            var phones = (long)Math.Floor(rub / prices.SmartphoneRub);
            list.Add(("📱", $"{phones} {RuFormat.Plural(phones, "смартфон", "смартфона", "смартфонов")}"));
        }
        if (prices.LaptopRub > 0m && rub >= prices.LaptopRub)
        {
            var macs = (long)Math.Floor(rub / prices.LaptopRub);
            list.Add(("💻", $"{macs} MacBook"));
        }
        return list;
    }
}
