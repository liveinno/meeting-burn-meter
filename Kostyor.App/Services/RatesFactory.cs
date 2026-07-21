using Kostyor.Core.Config;
using Kostyor.Core.Money;

namespace Kostyor.App.Services;

/// <summary>
/// Собирает <see cref="RatesService"/> с локальным курсом валют. Сеть отключена по требованию
/// владельца — онлайн-подтягивание ЦБ РФ убрано. Курс берётся из настроек пользователя
/// (<see cref="AppConfig.ManualRates"/>); дефолт — разумные значения.
/// </summary>
public static class RatesFactory
{
    public static RatesService Create(AppConfig config)
        => new RatesService(config.ManualRates.ToSnapshot(DateTimeOffset.Now));
}
