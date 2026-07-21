namespace Kostyor.Core.Roster;

/// <summary>Одна строка состава для расчёта: эффективная ставка ₽/час и количество людей.</summary>
public readonly record struct RoleRate(decimal RatePerHour, int Count);

/// <summary>Чистая математика состава — легко покрыть xUnit (ТЗ «Качество и логи»).</summary>
public static class RosterMath
{
    /// <summary>
    /// Ставка встречи ₽/час = Σ(ставка_роли × кол-во) × overhead (ТЗ §1).
    /// <paramref name="overhead"/> — коэффициент полной нагрузки (по умолчанию 1.0;
    /// 1.3–2.3 = «стоимость для компании, а не оклад»).
    /// </summary>
    public static decimal RatePerHour(IEnumerable<RoleRate> lines, decimal overhead = 1.0m)
    {
        if (lines is null) throw new ArgumentNullException(nameof(lines));
        if (overhead < 0m) overhead = 0m;
        decimal sum = 0m;
        foreach (var l in lines)
        {
            if (l.Count <= 0) continue;
            sum += l.RatePerHour * l.Count;
        }
        return sum * overhead;
    }

    /// <summary>Суммарное число участников.</summary>
    public static int Headcount(IEnumerable<RoleRate> lines)
    {
        if (lines is null) throw new ArgumentNullException(nameof(lines));
        var n = 0;
        foreach (var l in lines)
            if (l.Count > 0) n += l.Count;
        return n;
    }

    /// <summary>
    /// Помощник «оклад/мес → ставка/час»: <c>salary ÷ hoursPerMonth</c>
    /// (по умолчанию 160 часов, ТЗ §2). Неположительные входы → 0.
    /// </summary>
    public static decimal SalaryToHourly(decimal monthlySalary, decimal hoursPerMonth = 160m)
    {
        if (monthlySalary <= 0m || hoursPerMonth <= 0m) return 0m;
        return monthlySalary / hoursPerMonth;
    }
}
