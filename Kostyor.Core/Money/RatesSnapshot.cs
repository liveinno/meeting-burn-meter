namespace Kostyor.Core.Money;

/// <summary>Источник, из которого получен снимок курсов — для диагностики и UI.</summary>
public enum RatesOrigin
{
    /// <summary>Свежий ответ ЦБ РФ.</summary>
    Live,
    /// <summary>Файловый кеш прошлого успешного запроса.</summary>
    Cache,
    /// <summary>Дефолты из конфига (первый запуск без сети).</summary>
    Defaults,
    /// <summary>Курс задан пользователем вручную (не тянуть с ЦБ РФ).</summary>
    Manual
}

/// <summary>
/// Снимок курсов валют на определённую дату. Хранит рубль как базу и словарь
/// «код валюты → <see cref="CurrencyRate"/>». Кешируется в <c>%APPDATA%\Kostyor\rates.json</c>.
/// </summary>
public sealed record RatesSnapshot(
    DateTimeOffset Date,
    IReadOnlyDictionary<string, CurrencyRate> Rates,
    RatesOrigin Origin)
{
    /// <summary>Валюты, которые продукт показывает всегда (кроме базового рубля).</summary>
    public static readonly string[] DisplayCurrencies = { "USD", "EUR", "CNY" };

    public bool Has(string code) => Rates.ContainsKey(code);

    public CurrencyRate? Get(string code) =>
        Rates.TryGetValue(code, out var r) ? r : null;

    /// <summary>С тем же содержимым, но помеченный другим происхождением.</summary>
    public RatesSnapshot WithOrigin(RatesOrigin origin) => this with { Origin = origin };
}
