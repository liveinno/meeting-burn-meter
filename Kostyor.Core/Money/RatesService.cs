namespace Kostyor.Core.Money;

/// <summary>
/// Курс валют — <b>только локальный источник</b> (сеть не используется). Хранит текущий снимок
/// курсов, который задаёт пользователь в настройках (или разумный дефолт). Приложение никуда
/// не ходит за курсами — по требованию владельца онлайн-подтягивание (ЦБ РФ) убрано.
/// </summary>
public sealed class RatesService
{
    /// <summary>Актуальный снимок курсов (всегда не null).</summary>
    public RatesSnapshot Current { get; private set; }

    public RatesService(RatesSnapshot initial)
        => Current = initial ?? throw new ArgumentNullException(nameof(initial));

    /// <summary>Применить новый (заданный пользователем) курс.</summary>
    public void Apply(RatesSnapshot snapshot)
        => Current = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
}
