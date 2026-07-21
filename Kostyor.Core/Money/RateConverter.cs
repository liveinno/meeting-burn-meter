namespace Kostyor.Core.Money;

/// <summary>
/// Конвертация «рубли → валюта» <b>делением</b> с обязательным учётом <c>Nominal</c>
/// (ТЗ §1, AGENTS §12): <c>сумма_вал = сумма_₽ ÷ (Value ÷ Nominal)</c>.
/// Чистый класс без сети — тестируется детерминированно.
/// </summary>
public sealed class RateConverter
{
    private readonly IReadOnlyDictionary<string, CurrencyRate> _rates;

    public RateConverter(IReadOnlyDictionary<string, CurrencyRate> rates)
        => _rates = rates ?? throw new ArgumentNullException(nameof(rates));

    public RateConverter(RatesSnapshot snapshot)
        : this((snapshot ?? throw new ArgumentNullException(nameof(snapshot))).Rates) { }

    /// <summary>
    /// Возвращает сумму в валюте <paramref name="code"/> или <c>null</c>, если валюты нет
    /// в снимке или курс некорректен (защита от битого ответа ЦБ — ТЗ «Качество и логи»).
    /// </summary>
    public decimal? Convert(decimal rub, string code)
    {
        if (!_rates.TryGetValue(code, out var rate)) return null;
        var perUnit = rate.PerUnit;
        if (perUnit <= 0m) return null;
        return rub / perUnit;
    }
}
