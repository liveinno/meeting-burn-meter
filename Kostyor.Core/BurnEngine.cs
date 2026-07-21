namespace Kostyor.Core;

/// <summary>
/// Снимок состояния счётчика для персистентности (автосохранение раз в ~5 с, ТЗ §5).
/// Хранит только деньги/время/ставку; ростер и курсы — уровень приложения.
/// </summary>
public readonly record struct BurnState(
    decimal AccumulatedRub,
    double AccumulatedSeconds,
    decimal RatePerSecond,
    bool Running);

/// <summary>
/// Интегральный счётчик стоимости встречи — сердце продукта (AGENTS §4, ТЗ §1).
///
/// Состояние: <c>accumulatedRub</c>, <c>currentRatePerSec</c>, <c>runningSince</c>.
/// Отображаемая сумма = <c>accumulatedRub + (now − runningSince) × currentRatePerSec</c>.
/// Пауза и смена состава = <b>flush</b>: накопленное фиксируется, ставка пересчитывается,
/// отсчёт стартует заново «с этого момента» — задним числом ничего не пересчитывается.
///
/// Деньги и время считаются <b>от часов</b> (injectable clock), а не по тикам таймера —
/// одометр не «плывёт» на паузах и лагах (AGENTS §12).
/// </summary>
public sealed class BurnEngine
{
    private readonly Func<DateTimeOffset> _now;

    private decimal _accumulatedRub;
    private double _accumulatedSeconds;
    private decimal _ratePerSec;
    private DateTimeOffset? _runningSince;

    public BurnEngine(Func<DateTimeOffset>? clock = null)
        => _now = clock ?? (() => DateTimeOffset.UtcNow);

    public bool IsRunning => _runningSince is not null;

    /// <summary>Текущая ставка, ₽/сек.</summary>
    public decimal RatePerSecond => _ratePerSec;

    /// <summary>Текущая ставка, ₽/час.</summary>
    public decimal RatePerHour => _ratePerSec * 3600m;

    /// <summary>Накопленное прошедшее время встречи (сумма всех отрезков работы).</summary>
    public TimeSpan Elapsed =>
        TimeSpan.FromSeconds(_accumulatedSeconds + LiveSeconds());

    /// <summary>Текущая сожжённая сумма в рублях (интегральная).</summary>
    public decimal CurrentRub =>
        _accumulatedRub + (decimal)LiveSeconds() * _ratePerSec;

    /// <summary>Запустить/возобновить отсчёт. Повторный вызов на ходу игнорируется.</summary>
    public void Start()
    {
        if (_runningSince is not null) return;
        _runningSince = _now();
    }

    /// <summary>Пауза: зафиксировать накопленное и остановить отсчёт.</summary>
    public void Pause()
    {
        if (_runningSince is null) return;
        Flush();
        _runningSince = null;
    }

    /// <summary>
    /// Задать ставку ₽/час. Если счётчик идёт — сначала <b>flush</b>, поэтому новая ставка
    /// действует «с этого момента» (интегральный подсчёт при смене состава на лету).
    /// </summary>
    public void SetRatePerHour(decimal ratePerHour)
    {
        if (ratePerHour < 0m) ratePerHour = 0m;
        if (_runningSince is not null) Flush();
        _ratePerSec = ratePerHour / 3600m;
    }

    /// <summary>Полный сброс к нулю (кнопка «Новая встреча»).</summary>
    public void Reset()
    {
        _accumulatedRub = 0m;
        _accumulatedSeconds = 0d;
        _ratePerSec = 0m;
        _runningSince = null;
    }

    /// <summary>Снять снимок состояния (для автосохранения сессии).</summary>
    public BurnState Capture()
    {
        // Отдаём «сведённое» состояние, чтобы снимок не зависел от того, идёт ли счётчик.
        var live = LiveSeconds();
        return new BurnState(
            _accumulatedRub + (decimal)live * _ratePerSec,
            _accumulatedSeconds + live,
            _ratePerSec,
            _runningSince is not null);
    }

    /// <summary>
    /// Восстановить состояние из снимка. Если сессия была активной, отсчёт продолжится
    /// «с текущего момента» — время простоя (падение/перезапуск) в стоимость не попадёт.
    /// </summary>
    public void Restore(BurnState state)
    {
        _accumulatedRub = state.AccumulatedRub;
        _accumulatedSeconds = state.AccumulatedSeconds;
        _ratePerSec = state.RatePerSecond;
        _runningSince = state.Running ? _now() : null;
    }

    private double LiveSeconds()
    {
        if (_runningSince is null) return 0d;
        var s = (_now() - _runningSince.Value).TotalSeconds;
        return s > 0d ? s : 0d;
    }

    private void Flush()
    {
        if (_runningSince is null) return;
        var now = _now();
        var elapsed = (now - _runningSince.Value).TotalSeconds;
        if (elapsed < 0d) elapsed = 0d;
        _accumulatedRub += (decimal)elapsed * _ratePerSec;
        _accumulatedSeconds += elapsed;
        _runningSince = now;
    }
}
