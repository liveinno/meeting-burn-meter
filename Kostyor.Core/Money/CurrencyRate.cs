namespace Kostyor.Core.Money;

/// <summary>
/// Курс валюты в формате ЦБ РФ: <paramref name="Value"/> рублей за <paramref name="Nominal"/> единиц.
/// Цена одной единицы = <c>Value / Nominal</c>. Учитывать <c>Nominal</c> обязательно
/// (у CNY он исторически бывал ≠ 1) — иначе конвертация врёт (AGENTS §12, ТЗ §1).
/// </summary>
public readonly record struct CurrencyRate(decimal Value, int Nominal)
{
    /// <summary>Рублёвая цена одной единицы валюты (<c>Value / Nominal</c>).</summary>
    public decimal PerUnit => Nominal <= 0 ? 0m : Value / Nominal;
}
