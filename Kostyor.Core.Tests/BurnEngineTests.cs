using Kostyor.Core;
using Xunit;

namespace Kostyor.Core.Tests;

public class BurnEngineTests
{
    // 3600 ₽/час = ровно 1 ₽/сек — удобно для точных проверок.
    private const decimal OnePerSec = 3600m;

    [Fact]
    public void Accumulates_from_time_at_current_rate()
    {
        var clock = new TestClock();
        var engine = new BurnEngine(clock.Get);
        engine.SetRatePerHour(OnePerSec);
        engine.Start();

        clock.AdvanceSeconds(10);

        Assert.Equal(10m, engine.CurrentRub);
        Assert.Equal(TimeSpan.FromSeconds(10), engine.Elapsed);
        Assert.Equal(1m, engine.RatePerSecond);
    }

    [Fact]
    public void Roster_change_is_integral_not_retroactive()
    {
        var clock = new TestClock();
        var engine = new BurnEngine(clock.Get);
        engine.SetRatePerHour(OnePerSec); // 1 ₽/сек
        engine.Start();

        clock.AdvanceSeconds(10); // +10 ₽ по старой ставке

        engine.SetRatePerHour(OnePerSec * 2); // смена состава на лету → flush, теперь 2 ₽/сек
        clock.AdvanceSeconds(5); // +10 ₽ по новой ставке

        // Интегрально: 10*1 + 5*2 = 20. Ретроактивно было бы 15*2 = 30.
        Assert.Equal(20m, engine.CurrentRub);
        Assert.Equal(TimeSpan.FromSeconds(15), engine.Elapsed);
    }

    [Fact]
    public void Pause_stops_money_and_time_then_resumes()
    {
        var clock = new TestClock();
        var engine = new BurnEngine(clock.Get);
        engine.SetRatePerHour(OnePerSec);
        engine.Start();

        clock.AdvanceSeconds(10); // 10 ₽
        engine.Pause();

        clock.AdvanceSeconds(100); // на паузе ничего не капает
        Assert.Equal(10m, engine.CurrentRub);
        Assert.Equal(TimeSpan.FromSeconds(10), engine.Elapsed);
        Assert.False(engine.IsRunning);

        engine.Start();
        clock.AdvanceSeconds(5); // ещё 5 ₽
        Assert.Equal(15m, engine.CurrentRub);
        Assert.Equal(TimeSpan.FromSeconds(15), engine.Elapsed);
    }

    [Fact]
    public void Rate_change_while_paused_is_not_retroactive()
    {
        var clock = new TestClock();
        var engine = new BurnEngine(clock.Get);
        engine.SetRatePerHour(OnePerSec);
        engine.Start();
        clock.AdvanceSeconds(10); // 10 ₽ накоплено
        engine.Pause();

        engine.SetRatePerHour(OnePerSec * 10); // меняем ставку на паузе
        Assert.Equal(10m, engine.CurrentRub); // накопленное не переоценивается

        engine.Start();
        clock.AdvanceSeconds(1); // +10 ₽ по новой ставке
        Assert.Equal(20m, engine.CurrentRub);
    }

    [Fact]
    public void Second_start_call_does_not_reset_or_double()
    {
        var clock = new TestClock();
        var engine = new BurnEngine(clock.Get);
        engine.SetRatePerHour(OnePerSec);
        engine.Start();
        clock.AdvanceSeconds(5);
        engine.Start(); // повторный старт на ходу — игнор
        clock.AdvanceSeconds(5);
        Assert.Equal(10m, engine.CurrentRub);
    }

    [Fact]
    public void SetRate_before_start_takes_effect_on_run()
    {
        var clock = new TestClock();
        var engine = new BurnEngine(clock.Get);
        engine.SetRatePerHour(7200m); // 2 ₽/сек ещё до старта
        engine.Start();
        clock.AdvanceSeconds(3);
        Assert.Equal(6m, engine.CurrentRub);
    }

    [Fact]
    public void Reset_clears_everything()
    {
        var clock = new TestClock();
        var engine = new BurnEngine(clock.Get);
        engine.SetRatePerHour(OnePerSec);
        engine.Start();
        clock.AdvanceSeconds(30);
        engine.Reset();

        Assert.Equal(0m, engine.CurrentRub);
        Assert.Equal(TimeSpan.Zero, engine.Elapsed);
        Assert.False(engine.IsRunning);
        Assert.Equal(0m, engine.RatePerSecond);
    }

    [Fact]
    public void Capture_then_restore_preserves_amount_and_continues()
    {
        var clock = new TestClock();
        var engine = new BurnEngine(clock.Get);
        engine.SetRatePerHour(OnePerSec);
        engine.Start();
        clock.AdvanceSeconds(42);

        var state = engine.Capture();
        Assert.Equal(42m, state.AccumulatedRub);
        Assert.True(state.Running);

        // Симуляция перезапуска: новый движок, состояние восстановлено.
        var clock2 = new TestClock(clock.Now + TimeSpan.FromMinutes(3)); // время простоя
        var restored = new BurnEngine(clock2.Get);
        restored.Restore(state);

        Assert.Equal(42m, restored.CurrentRub); // простой в стоимость не попал
        clock2.AdvanceSeconds(8);
        Assert.Equal(50m, restored.CurrentRub); // счёт продолжился «с этого момента»
        Assert.Equal(TimeSpan.FromSeconds(50), restored.Elapsed);
    }

    [Fact]
    public void Negative_rate_is_clamped_to_zero()
    {
        var engine = new BurnEngine(new TestClock().Get);
        engine.SetRatePerHour(-5000m);
        Assert.Equal(0m, engine.RatePerSecond);
    }

    [Fact]
    public void RatePerHour_roundtrips_through_per_second()
    {
        var engine = new BurnEngine(new TestClock().Get);
        engine.SetRatePerHour(9000m);
        Assert.Equal(9000m, engine.RatePerHour);
        Assert.Equal(2.5m, engine.RatePerSecond);
    }
}
